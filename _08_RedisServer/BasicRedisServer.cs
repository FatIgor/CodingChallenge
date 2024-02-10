using System.Net;
using System.Net.Sockets;
using System.Text;

namespace _08_RedisServer;

public class BasicRedisServer
{
    private readonly IPAddress _ipAddress;
    private readonly int _port;
    private readonly TcpListener _listener;
    private string _currentKey = "";
    private IReadOnlyList<object>? _receivedCommand;
    private readonly RESP _respHandler = new();
    private int _receivedCommandCount;
    
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
            var threadObject = new ThreadHandler();
            var clientThread = new Thread(threadObject.ClientThread!);
            clientThread.Start(client);
        }
    }


}