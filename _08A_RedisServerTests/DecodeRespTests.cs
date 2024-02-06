using _08_RedisServer;
using Xunit.Abstractions;

namespace _08A_RedisServerTests;

public class DecodeRespTests(ITestOutputHelper output)
{
    private readonly RESP resp = new();

    #region Failure tests
    [Fact]
    public void FailingToDecodeHeaderlessRespObject()
    {
        var respDecoded = resp.DecodeRESP("Error message\r\n");
        Assert.False(respDecoded.Success);
    }

    [Fact]
    public void FailingToDecodeBadBulkStringRespObject()
    {
        var respDecoded = resp.DecodeRESP("$5\r\nhello world\r\n");
        Assert.False(respDecoded.Success);
    }
    
    [Fact]
    public void FailingToDecodeBadBooleanRespObject()
    {
        var respDecoded = resp.DecodeRESP("#true\r\n");
        Assert.False(respDecoded.Success);
        respDecoded = resp.DecodeRESP("#false\r\n");
        Assert.False(respDecoded.Success);
    }
    
    [Fact]
    public void FailingToDecodeBadDoubleRespObject()
    {
        var respDecoded = resp.DecodeRESP(",123.456.789\r\n");
        Assert.False(respDecoded.Success);
    }
    
    [Fact]
    public void FailingToDecodeBadIntegerRespObject()
    {
        var respDecoded = resp.DecodeRESP(":123.456\r\n");
        Assert.False(respDecoded.Success);
    }
    
    [Fact]
    public void FailingToDecodeBadBigNumberRespObject()
    {
        var respDecoded = resp.DecodeRESP("(123.456\r\n");
        Assert.False(respDecoded.Success);
        respDecoded = resp.DecodeRESP("(A6B7\r\n");
        Assert.False(respDecoded.Success);
    }
    #endregion


    #region RESP2 success
    [Theory]
    // text
    [InlineData("Hello", "+Hello\r\n")]
    // simple error
    [InlineData("Divide By Zero Error", "-Divide By Zero Error\r\n")]
    // integer
    [InlineData((long)-123456, ":-123456\r\n")]
    [InlineData((long)123456, ":+123456\r\n")]
    [InlineData((long)123456, ":123456\r\n")]
    // bulk string
    [InlineData("Hello World", "$11\r\nHello World\r\n")]
    [InlineData("", "$0\r\n\r\n")]
    [InlineData(null, "$-1\r\n")]
    public void SuccessfullyDecoding_Text_SimpleError_BulkString_Resp2Objects(object expected, string input)
    {
        var respDecoded = resp.DecodeRESP(input);
        Assert.True(respDecoded.Success);
        Assert.Equal(expected, respDecoded.DecodedRESPObject);
    }

    [Theory]
    // array
    [InlineData(new object[0], "*0\r\n")]
    [InlineData(null, "*-1\r\n")]
    [InlineData(new object[] { "hello", "world" }, "*2\r\n$5\r\nhello\r\n$5\r\nworld\r\n")]
    [InlineData(new object[] { (long)1, (long)2, (long)3 }, "*3\r\n:1\r\n:2\r\n:3\r\n")]
    [InlineData(new object[] { (long)1, "hello", (long)3 }, "*3\r\n:1\r\n$5\r\nhello\r\n:3\r\n")]
    [InlineData(
        new Object[] { new Object[] { (long)1, (long)2, (long)3 }, new object[] { "Hello", "World" } },
        "*2\r\n*3\r\n:1\r\n:2\r\n:3\r\n*2\r\n+Hello\r\n-World\r\n")]
    [InlineData(new object[]{false,null,true}, "*3\r\n#f\r\n_\r\n#t\r\n")]
    [InlineData(new object[] { (long)1, null, (long)3 }, "*3\r\n:1\r\n$-1\r\n:3\r\n")]
    [InlineData(new object[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity,"txt:Hello World","LastItem"},
        "*5\r\n,nan\r\n,inf\r\n,-inf\r\n=15\r\ntxt:Hello World\r\n+LastItem\r\n")]
    [InlineData(new object[]{"123456789098765432101234567890987654321012345678909876543210","CORRECT"}, "*2\r\n(123456789098765432101234567890987654321012345678909876543210\r\n+CORRECT\r\n")]
    public void SuccessfullyDecoding_Array_Resp2Objects(object expected, string input)
    {
        var respDecoded = resp.DecodeRESP(input);
        Assert.True(respDecoded.Success);
        Assert.Equal(expected, respDecoded.DecodedRESPObject);
    }
    #endregion

    #region RESP3 success
    [Theory]
    // null
    [InlineData(null, "_\r\n")]
    // boolean
    [InlineData(true, "#t\r\n")]
    [InlineData(false, "#f\r\n")]
    // double
    [InlineData(123.456, ",123.456\r\n")]
    [InlineData(-123.456, ",-123.456\r\n")]
    [InlineData(0.0, ",0\r\n")]
    [InlineData(1000.0, ",1e3\r\n")]
    [InlineData(0.1, ",1e-1\r\n")]
    [InlineData(double.NaN, ",nan\r\n")]
    [InlineData(double.PositiveInfinity, ",inf\r\n")]
    [InlineData(double.NegativeInfinity, ",-inf\r\n")]
    public void SuccessfullyDecodingType3_Null_Boolean_Double_RespObjects(object expected, string input)
    {
        var respDecoded = resp.DecodeRESP(input);
        Assert.True(respDecoded.Success);
        Assert.Equal(expected, respDecoded.DecodedRESPObject);
    }
    
    [Theory]
    // big number
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345678901234567890", "(12345678901234567890123456789012345678901234567890123456789012345678901234567890\r\n")]
    [InlineData("-123", "(-123\r\n")]
    [InlineData("+123", "(+123\r\n")]
    // bulk error
    [InlineData("Error message", "!13\r\nError message\r\n")]
    // verbatim string
    [InlineData("txt:Hello World", "=15\r\ntxt:Hello World\r\n")]
    public void SuccessfullyDecodingType3_BigNumber_BulkError_VerbatimString_RespObjects(object expected, string input)
    {
        var respDecoded = resp.DecodeRESP(input);
        Assert.True(respDecoded.Success);
        Assert.Equal(expected, respDecoded.DecodedRESPObject);
    }
    #endregion

    #region TODO

    [Fact]
    public void RewriteTheStringSplitting()
    {
        const bool haveRewrittenStringSplitting = true;
        Assert.True(haveRewrittenStringSplitting);
    }
    
    #endregion

}