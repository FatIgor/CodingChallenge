using System.Diagnostics.CodeAnalysis;

namespace _08_RedisServer;

public class RESPEncoded
{
    public bool Success { get; set; }
    [SuppressMessage("ReSharper", "InconsistentNaming")] 
    public string EncodedRESP { get; set; }=string.Empty;
    public string ErrorMessage { get; set; }=string.Empty;
}