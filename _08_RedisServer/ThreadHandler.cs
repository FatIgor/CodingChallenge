using System.Net.Sockets;
using System.Text;

namespace _08_RedisServer;

public class ThreadHandler
{
    private string _currentKey = "";
    private IReadOnlyList<object>? _receivedCommand;
    private readonly RESP _respHandler = new();
    private int _receivedCommandCount;

    private static Dictionary<string, object> _store = new();
    private static Dictionary<string, DateTime> _expiry = new();

    private static int threadCount=0;
        
    public async void ClientThread(object clientObj)
    {
        threadCount++;
        Console.WriteLine(threadCount);
        var client = (TcpClient)clientObj;
        var keepListening = true;
        while (keepListening)
        {
            var commandResponse = string.Empty;
            var stream = client.GetStream();
            var buffer = new byte[client.ReceiveBufferSize];
            if (client.ReceiveBufferSize == 0)
            {
                keepListening = false;
                break;
            }
            int bytesRead=0;

            try
            {
                bytesRead = stream.Read(buffer, 0, client.ReceiveBufferSize);
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Connection reset by peer"))
                {
                    keepListening = false;
                    break;
                }
            }
            if (bytesRead== 0)
            {
                keepListening = false;
            }

            var request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
//            Console.WriteLine($"request: {request}");
            var decodedRESP = _respHandler.DecodeRESP(request);
            if (decodedRESP.Success)
            {
                _receivedCommand = (object[])decodedRESP.DecodedRESPObject!;
                _receivedCommandCount = _receivedCommand.Count;
                var lockObject = new object();
                lock (lockObject)
                { 
                    commandResponse= HandleCommand();
                }
            }
            else
            {
                commandResponse = $"-Error: {decodedRESP.ErrorMessage}\r\n";
            }
            client.Client.Send(Encoding.UTF8.GetBytes(commandResponse), SocketFlags.None);
        }

        threadCount--;
        client.Close();
        Console.WriteLine("Client disconnected");
        Console.WriteLine(threadCount);
    }

    private string HandleCommand()
    {
        if (_receivedCommandCount == 0)
            return "";
        var command = _receivedCommand[0].ToString()!.ToLower();
        switch (command)
        {
            case "ping":
                return _respHandler.SingleItemEncodeRESP("PONG", RESP.RespType.SimpleString).EncodedRESP;
            case "echo" when _receivedCommandCount == 1:
                return _respHandler.SingleItemEncodeRESP("ECHO: wrong number of arguments", RESP.RespType.SimpleError)
                    .EncodedRESP;
            case "echo":
            {
                var echoMessage = string.Empty;
                string separator = "";
                for (int i = 1; i < _receivedCommandCount; i++)
                {
                    echoMessage += separator + _receivedCommand[i];
                    separator = " ";
                }

                return _respHandler.SingleItemEncodeRESP(echoMessage, RESP.RespType.BulkString).EncodedRESP;
            }
            case "set" when _receivedCommandCount < 3:
                return _respHandler.SingleItemEncodeRESP("SET: not enough arguments", RESP.RespType.SimpleError)
                    .EncodedRESP;
            case "set":
            {
                _currentKey = _receivedCommand[1].ToString();
                var value = _receivedCommand[2];
                if (_receivedCommandCount == 3)
                    return DoStraightSet(value);
                var returnString = ProcessParametersFromSetCommand();
                if ((byte)returnString[0] == (byte)RESP.RespType.SimpleError)
                    return returnString;
                var nxSet = CheckForCommandValue("nx");
                var xxSet = CheckForCommandValue("xx");
                CheckForExpiry();
                switch (nxSet)
                {
                    case true when xxSet:
                        return _respHandler.SingleItemEncodeRESP("SET: both NX and XX set", RESP.RespType.SimpleError)
                            .EncodedRESP;
                    case true when _store.ContainsKey(_currentKey):
                        return _respHandler.SingleItemEncodeRESP("SET: key already exists", RESP.RespType.SimpleError)
                            .EncodedRESP;
                }

                if (xxSet && !_store.ContainsKey(_currentKey))
                    return _respHandler.SingleItemEncodeRESP("SET: key does not exist", RESP.RespType.SimpleError)
                        .EncodedRESP;
                return DoStraightSet(value);
            }
            case "get" when _receivedCommandCount != 2:
                return _respHandler.SingleItemEncodeRESP("GET: wrong number of arguments", RESP.RespType.SimpleError)
                    .EncodedRESP;
            case "get":
            {
                _currentKey = _receivedCommand[1].ToString();
                if (_currentKey == "IANSAYSGETALL")
                {
                    foreach (var k in _store.Keys)
                    {
                        Console.WriteLine($"{k}={_store[k]}");
                    }

                    return _respHandler.SingleItemEncodeRESP("OK", RESP.RespType.SimpleString).EncodedRESP;
                }

                CheckForExpiry();
                return _currentKey != null && _store.TryGetValue(_currentKey, out var value)
                    ? _respHandler.SingleItemEncodeRESP(value, RESP.RespType.SimpleString).EncodedRESP
                    : _respHandler.SingleItemEncodeRESP(null, RESP.RespType.Null).EncodedRESP;
            }
            default:
                return _respHandler.SingleItemEncodeRESP("Unknown command", RESP.RespType.SimpleError).EncodedRESP;
        }
    }

    private string ProcessParametersFromSetCommand()
    {
        var returnString = CheckForExpiryTime();
        if ((byte)returnString[0] == (byte)RESP.RespType.SimpleError)
            return returnString;
        if (CheckForCommandValue("GET"))
        {
            CheckForExpiry();
            _store.TryGetValue(_currentKey, out var getValue);
            returnString = (string)getValue! ?? _respHandler.SingleItemEncodeRESP(null, RESP.RespType.Null).EncodedRESP;
        }
        return returnString;
    }

    private bool CheckForCommandValue(string command)
    {
        for (var setIndex = 3; setIndex < _receivedCommandCount; setIndex++)
        {
            if (_receivedCommand[setIndex].ToString().ToLower() == command)
            {
                return true;
            }
        }

        return false;
    }

    private string CheckForExpiryTime()
    {
        var returnString = "OK";

        for (var setIndex = 3; setIndex < _receivedCommandCount; setIndex++)
        {
            switch (_receivedCommand[setIndex].ToString().ToLower())
            {
                case "px":
                    if (_receivedCommandCount <= setIndex + 1)
                        return _respHandler.SingleItemEncodeRESP("SET: PX without value", RESP.RespType.SimpleError)
                            .EncodedRESP;
                    if (int.TryParse(_receivedCommand[setIndex + 1].ToString(), out var pxExpiryMilliseconds))
                    {
                        if (pxExpiryMilliseconds <= 0)
                            return _respHandler.SingleItemEncodeRESP("SET: PX with non-positive value",
                                    RESP.RespType.SimpleError)
                                .EncodedRESP;
                    }
                    else
                    {
                        return _respHandler.SingleItemEncodeRESP("SET: PX with non-integer value",
                                RESP.RespType.SimpleError)
                            .EncodedRESP;
                    }

                    SetExpiryTime(pxExpiryMilliseconds);
                    break;
                case "ex":
                    if (_receivedCommandCount <= setIndex + 1)
                        return _respHandler.SingleItemEncodeRESP("SET: PX without value", RESP.RespType.SimpleError)
                            .EncodedRESP;
                    if (int.TryParse(_receivedCommand[setIndex + 1].ToString(), out var exExpirySeconds))
                    {
                        if (exExpirySeconds <= 0)
                            return _respHandler.SingleItemEncodeRESP("SET: EX with non-positive value",
                                    RESP.RespType.SimpleError)
                                .EncodedRESP;
                    }
                    else
                    {
                        return _respHandler.SingleItemEncodeRESP("SET: EX with non-integer value",
                                RESP.RespType.SimpleError)
                            .EncodedRESP;
                    }

                    SetExpiryTime(exExpirySeconds * 1000);
                    break;
            }
        }

        return returnString;
    }

    private void SetExpiryTime(int pxExpiryMilliseconds)
    {
        _expiry[_currentKey] = DateTime.Now.AddMilliseconds(pxExpiryMilliseconds);
    }

    private void CheckForExpiry()
    {
        if (!_expiry.TryGetValue(_currentKey, out var expiryTime) || expiryTime >= DateTime.Now) return;
        _store.Remove(_currentKey);
        _expiry.Remove(_currentKey);
    }

    private string DoStraightSet(object value)
    {
        _expiry.Remove(_currentKey);
        _store[_currentKey] = value;
        return _respHandler.SingleItemEncodeRESP("OK", RESP.RespType.SimpleString).EncodedRESP;
    }
}