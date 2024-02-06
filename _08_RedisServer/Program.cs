using _08_RedisServer;

var resp = new RESP();
var obj1=resp.DecodeRESP("$0\r\n\r\n");
var obj2=resp.DecodeRESP("*2\r\n$5\r\nhello\r\n$5\r\nworld\r\n");

var enc1 = resp.SingleItemEncodeRESP(obj1.DecodedRESPObject, RESP.RespType.BulkString);
var enc2 = resp.SingleItemEncodeRESP(123456, RESP.RespType.Integer);
var enc3 = resp.SingleItemEncodeRESP(123.456, RESP.RespType.Double);
var enc4 = resp.SingleItemEncodeRESP(true, RESP.RespType.Boolean);
var type = RESP.RespType.BulkString;
Console.WriteLine($"{(char)type}");
Console.WriteLine("Hello");

var listener=new BasicRedisServer(null);
await listener.StartServer();