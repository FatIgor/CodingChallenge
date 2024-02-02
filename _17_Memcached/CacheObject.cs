namespace _17_Memcached;

public class CacheObject
{
    public string? Key { get; set; }
    public short Flags { get; set; }
    public int ExpTime { get; set; }
    public DateTime? StoredDateTime { get; set; }
    public short ByteCount { get; set; }
    public string DataBlock { get; set; }=string.Empty;
}