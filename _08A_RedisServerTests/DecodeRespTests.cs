using _08_RedisServer;
using Xunit.Abstractions;

namespace _08A_RedisServerTests;

public class DecodeRespTests(ITestOutputHelper output)
{
    //
    // This section is RESP2
    //

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
    public void SuccessfullySimpleRespObjects(object expected, string input)
    {
        var respConverter = new RESP();
        var respDecoded = respConverter.DecodeResp(input);
        Assert.True(respDecoded.Success);
        output.WriteLine($"{expected} {respDecoded.ResponseObject}");
        Assert.Equal(expected, respDecoded.ResponseObject);
    }

    [Theory]
    // array
    [InlineData(new object[0], "*0\r\n")]
    [InlineData(new object[] { "hello", "world" }, "*2\r\n$5\r\nhello\r\n$5\r\nworld\r\n")]
    [InlineData(new object[] { (long)1, (long)2, (long)3 }, "*3\r\n:1\r\n:2\r\n:3\r\n")]
    [InlineData(new object[] { (long)1, "hello", (long)3 }, "*3\r\n:1\r\n$5\r\nhello\r\n:3\r\n")]
    [InlineData(
        new Object[] {  new Object[] { (long)1, (long)2, (long)3 }, new object[] { "Hello", "World" } },
        "*2\r\n*3\r\n:1\r\n:2\r\n:3\r\n*2\r\n+Hello\r\n-World\r\n")]
    public void SuccessfullyArrayRespObjects(object expected, string input)
    {
        var a = new Object[] { new object[] { new Object[] { 1, 2, 3 } }, new object[] { "Hello", "World" } };
        var respConverter = new RESP();
        var respDecoded = respConverter.DecodeResp(input);
        Assert.True(respDecoded.Success);
        output.WriteLine($"{expected} {respDecoded.ResponseObject}");
        Assert.Equal(expected, respDecoded.ResponseObject);
    }

    [Fact]
    public void FailingToDecodeRespObjects()
    {
        var respConverter = new RESP();
        var respDecoded = respConverter.DecodeResp("Error message\r\n");
        Assert.False(respDecoded.Success);
    }
}