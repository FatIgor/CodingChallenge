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

    private string HandleCommand(object[] receivedCommand)
    {
        if (receivedCommand.Length == 0)
            return "";
        var command = receivedCommand[0].ToString().ToLower();
        switch (command)
        {
            case "ping":
                return "+PONG\r\n";
            case "echo" when receivedCommand.Length==1:
                return "-ECHO: wrong number of arguments\r\n";
            case "echo":
            {
                var echoMessage = string.Empty;
                for (int i=1;i<receivedCommand.Length;i++)
                {
                    echoMessage+=receivedCommand[i];
                }
                echoMessage=$"${echoMessage.Length}\r\n{echoMessage}\r\n";
                return echoMessage;
            }
            default:
                return $"-Unknown command '{command}'\r\n";
        }
    }
}