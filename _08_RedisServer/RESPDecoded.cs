namespace _08_RedisServer;

public class RESPDecoded
{
    public bool Success { get; set; }
    public object? ResponseObject { get; set; }
    public string ErrorMessage { get; set; }=string.Empty;
}