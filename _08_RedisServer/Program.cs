using _08_RedisServer;
var respObj = new RESP();
var obj1=respObj.DecodeResp("$0\r\n\r\n");
var obj2=respObj.DecodeResp("*2\r\n$5\r\nhello\r\n$5\r\nworld\r\n");