using System.Net;
using System.Net.Sockets;
using System.Text;

namespace _17_Memcached;

public class BasicServer
{
    private readonly IPAddress _ipAddress;
    private readonly int _port;
    private readonly TcpListener _listener;
    private readonly List<CacheObject> _cache = [];

    public BasicServer(IPAddress? ipAddress, int port = 0)
    {
        ipAddress ??= new IPAddress(new byte[] { 127, 0, 0, 1 });
        if (port == 0)
        {
            port = 11211;
        }

        _ipAddress = ipAddress;
        _port = port;
        _listener = new TcpListener(_ipAddress, _port);
    }

    public async Task<bool> StartServer()
    {
        _listener.Start();
        Console.WriteLine($"Server started on {_ipAddress}:{_port}");
        while (true)
        {
            var client = await _listener.AcceptTcpClientAsync();
            Console.WriteLine("Client connected");
            var clientThread = new Thread(ClientThread!);
            clientThread.Start(client);
        }
    }

    private void ClientThread(object clientObj)
    {
        var client = (TcpClient)clientObj;
        var keepListening = true;
        while (keepListening)
        {
            var strResponse = new string("");
            var stream = client.GetStream();
            var buffer = new byte[client.ReceiveBufferSize];
            var bytesRead = stream.Read(buffer, 0, client.ReceiveBufferSize);
            var request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            request = request.Replace("\r\n", "");
            var requestParts = request.Split(' ');
            string requestValue;
            switch (requestParts[0])
            {
                case "set":
                    requestValue = GetRequestValue(client, stream);
                    strResponse = HandleSet(requestParts, requestValue);
                    break;
                case "add":
                    requestValue = GetRequestValue(client, stream);
                    strResponse = HandleAdd(requestParts, requestValue);
                    break;
                case "replace":
                    requestValue = GetRequestValue(client, stream);
                    strResponse = HandleReplace(requestParts, requestValue);
                    break;
                case "append":
                    requestValue = GetRequestValue(client, stream);
                    strResponse = HandlePreApPend(requestParts, requestValue,false);
                    break;
                case "prepend":
                    requestValue = GetRequestValue(client, stream);
                    strResponse = HandlePreApPend(requestParts, requestValue,true);
                    break;
                case "get":
                    strResponse = HandleGet(requestParts);
                    break;
                case "QUIT":
                    keepListening = false;
                    break;
                default:
                    Console.WriteLine($"Unknown request: {request}");
                    break;
            }

            if (strResponse == "") continue;
            var response = Encoding.ASCII.GetBytes(strResponse);
            stream.Write(response, 0, response.Length);
        }

        client.Close();
        Console.WriteLine("Client disconnected");
    }

    private static string GetRequestValue(TcpClient? client, NetworkStream? stream)
    {
        var buffer2 = new byte[client!.ReceiveBufferSize];
        var bytesRead2 = stream!.Read(buffer2, 0, client.ReceiveBufferSize);
        var requestValue = Encoding.ASCII.GetString(buffer2, 0, bytesRead2).Replace("\r\n", "");
        return requestValue;
    }

    private string HandlePreApPend(IReadOnlyList<string> parts, string requestValue,bool prepend)
    {
        if (parts.Count != 5 && parts.Count != 6)
        {
            return "NOT_STORED\r\n";
        }
        if (requestValue.Length!=short.Parse(parts[4]))
        {
            return "NOT_STORED\r\n";
        }
        foreach (var obj in _cache.Where(obj => obj.Key == parts[1]))
        {
            obj.DataBlock = prepend ? requestValue + obj.DataBlock : obj.DataBlock + requestValue;
            obj.ByteCount+=short.Parse(parts[4]);
            return "STORED\r\n";
        }
        return "NOT_STORED\r\n";
    }
    
    private string HandleAdd(IReadOnlyList<string> parts, string requestValue)
    {
        return _cache.Any(obj => obj.Key == parts[1]) ? "NOT_STORED\r\n" : HandleSet(parts, requestValue);
    }

    private string HandleReplace(IReadOnlyList<string> parts, string requestValue)
    {
        foreach (var obj in _cache.Where(obj => obj.Key == parts[1]))
        {
            if (obj.ByteCount != requestValue.Length) continue;
            obj.DataBlock = requestValue;
            return "STORED\r\n";
        }

        return "NOT_STORED\r\n";
    }

    private string HandleSet(IReadOnlyList<string> parts, string requestValue)
    {
        if (parts.Count != 5 && parts.Count != 6)
        {
            return "NOT_STORED\r\n";
        }

        var key = parts[1];
        var flags = short.Parse(parts[2]);
        var expTime = int.Parse(parts[3]);
        var byteCount = short.Parse(parts[4]);
        var newCacheObject = new CacheObject
        {
            Key = key,
            Flags = flags,
            ExpTime = expTime,
            StoredDateTime = DateTime.UtcNow,
            ByteCount = byteCount,
            DataBlock = requestValue
        };
        if (newCacheObject.DataBlock.Length != newCacheObject.ByteCount)
        {
            return "NOT_STORED\r\n";
        }

        _cache.Add(newCacheObject);
        if (parts.Count != 7) return "STORED\r\n";
        return parts[5] == "noreply" ? "" : "NOT_STORED\r\n";
    }

    private string HandleGet(IReadOnlyList<string> parts)
    {
        if (parts.Count != 2)
        {
            return "END\r\n";
        }

        var key = parts[1];
        var cacheObject = _cache.FirstOrDefault(c => c.Key == key);
        if (cacheObject==null || (cacheObject.ExpTime < 0) ||(cacheObject.ExpTime>0 && cacheObject.StoredDateTime!.Value.AddSeconds(cacheObject.ExpTime) < DateTime.UtcNow))
        {
            return "END\r\n";
        }

        return $"VALUE {cacheObject?.DataBlock} {cacheObject!.Flags} {cacheObject.ByteCount}\r\n";
    }
}