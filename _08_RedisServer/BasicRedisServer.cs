using System.Net;
using System.Net.Sockets;
using System.Text;

namespace _08_RedisServer;

public class BasicRedisServer
{
    private readonly IPAddress _ipAddress;
    private readonly int _port;
    private readonly TcpListener _listener;

    public BasicRedisServer(IPAddress? ipAddress, int port = 0)
    {
        ipAddress ??= new IPAddress(new byte[] { 127, 0, 0, 1 });
        if (port == 0)
        {
            port = 6379;
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
        var respHandler = new RESP();
        var keepListening = true;
        while (keepListening)
        {
            var response = string.Empty;
            var stream = client.GetStream();
            var buffer = new byte[client.ReceiveBufferSize];
            var bytesRead = stream.Read(buffer, 0, client.ReceiveBufferSize);
            var request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            var command=respHandler.DecodeRESP(request);
            if (command.Success)
            {
                response = HandleCommand((object[])command.DecodedRESPObject);
                var r2=client.Client.Send(Encoding.UTF8.GetBytes(response),SocketFlags.None);
            }
            else
            {
                response = $"-Error: {command.ErrorMessage}\r\n";
            }
        }

        client.Close();
        Console.WriteLine("Client disconnected");
    }

    private Dictionary<string,object> _store=new();
    
    private string HandleCommand(object[] receivedCommand)
    {
        var respHandler = new RESP();
        if (receivedCommand.Length == 0)
            return "";
        var command = receivedCommand[0].ToString().ToLower();
        switch (command)
        {
            case "ping":
                return respHandler.SingleItemEncodeRESP("PONG", RESP.RespType.SimpleString).EncodedRESP;
            case "echo" when receivedCommand.Length==1:
                return respHandler.SingleItemEncodeRESP("ECHO: wrong number of arguments", RESP.RespType.SimpleError).EncodedRESP;
            case "echo":
            {
                var echoMessage = string.Empty;
                string separator = "";
                for (int i=1;i<receivedCommand.Length;i++)
                {
                    echoMessage+=separator+receivedCommand[i];
                    separator=" ";
                }
                return respHandler.SingleItemEncodeRESP(echoMessage, RESP.RespType.BulkString).EncodedRESP;
            }
            case "set" when receivedCommand.Length!=3:
                return respHandler.SingleItemEncodeRESP("SET: wrong number of arguments", RESP.RespType.SimpleError).EncodedRESP;
            case "set":
            {
                var key = receivedCommand[1].ToString();
                var value = receivedCommand[2];
                _store[key] = value;
                return respHandler.SingleItemEncodeRESP("OK", RESP.RespType.SimpleString).EncodedRESP;
            }
            case "get" when receivedCommand.Length!=2:
                return respHandler.SingleItemEncodeRESP("GET: wrong number of arguments", RESP.RespType.SimpleError).EncodedRESP;
            case "get":
            {
                var key = receivedCommand[1].ToString();
                return key != null && _store.TryGetValue(key, out var value) ? respHandler.SingleItemEncodeRESP(value, RESP.RespType.BulkString).EncodedRESP : respHandler.SingleItemEncodeRESP(null, RESP.RespType.BulkString).EncodedRESP;
            }
            default:
                return respHandler.SingleItemEncodeRESP("Unknown command", RESP.RespType.SimpleError).EncodedRESP;
        }
    }
}