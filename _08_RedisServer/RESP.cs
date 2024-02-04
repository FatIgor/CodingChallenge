using System.Runtime.InteropServices.JavaScript;
using System.Text;

namespace _08_RedisServer;

public class RESP
{
    private enum RespType
    {
        SimpleString = '+',
        SimpleError = '-',
        Integer = ':',
        BulkString = '$',
        Array = '*',
        Null = '_',
        Boolean = '#',
        Double = ',',
        BigNumber = '(',
        BulkError = '!',
        VerbatimString = '=',
        Map = '%',
        Set = '~',
        Push = '>'
    }

    private void Error(RespType type, string message)
    {
        var typeName = Enum.GetName(typeof(RespType), type);
        var errorMessage = $"{typeName} {message}";
        Console.WriteLine(errorMessage);
        throw new Exception(errorMessage);
    }

    public RESPDecoded DecodeResp(string respStringToDecode)
    {
        var splitVersionOfRespString = respStringToDecode.Replace("\r", "").Split('\n');
        return DecodeRespMainPart(splitVersionOfRespString, 0);
    }

    private RESPDecoded DecodeRespMainPart(IReadOnlyList<string> splitVersionOfRespString, int currentSplitIndex)
    {
        var respStringToDecode = splitVersionOfRespString[currentSplitIndex];
        var response = new RESPDecoded();
        try
        {
            switch ((byte)respStringToDecode[0])
            {
                case (byte)RespType.SimpleString:
                case (byte)RespType.SimpleError:
                    response.Success = true;
                    response.ResponseObject = splitVersionOfRespString[currentSplitIndex][1..];
                    break;
                case (byte)RespType.Integer:
                    response.Success = true;
                    response.ResponseObject = long.Parse(splitVersionOfRespString[currentSplitIndex][1..]);
                    break;
                case (byte)RespType.BulkString:
                    response.Success = true;
                    if (respStringToDecode == "$-1")
                    {
                        response.ResponseObject = null;
                        break;
                    }
                    response.ResponseObject = splitVersionOfRespString[currentSplitIndex+1];
                    break;
                case (byte)RespType.Array:
                    var arrayLength = int.Parse(splitVersionOfRespString[currentSplitIndex][1..]);
                    var objectArray= new object[arrayLength];
                    var arrayIndex = 1;
                    RESPDecoded resp;
                    for (var i = 0; i < arrayLength; i++)
                    {
                        if (splitVersionOfRespString[arrayIndex][0] == (byte)RespType.Array)
                        {
                            var newArrayLength = int.Parse(splitVersionOfRespString[arrayIndex][1..]);
                            var respDecoder = new RESP();
                            var stringToDecode = "";
                            for (var j=arrayIndex; j<arrayIndex+newArrayLength+1; j++)
                            {
                                stringToDecode += splitVersionOfRespString[j] + "\r\n";
                            }

                            arrayIndex += arrayLength+1;
                            resp= respDecoder.DecodeResp(stringToDecode);
                        }
                        else
                        {
                            resp = DecodeRespMainPart(splitVersionOfRespString, arrayIndex);
                        }

                        if (!resp.Success)
                        {
                            response.Success = false;
                            response.ErrorMessage = resp.ErrorMessage;
                            return response;
                        }
                        objectArray[i]=resp.ResponseObject;
                        if (splitVersionOfRespString[arrayIndex].Length==0)
                        {
                            arrayIndex++;
                        }
                        else
                        {
                            var currentRespType = splitVersionOfRespString[arrayIndex][0];
                            if (currentRespType == (byte)RespType.BulkString)
                            {
                                arrayIndex += 2;
                            }
                            else
                            {
                                arrayIndex++;
                            }
                        }
                    }
                    response.ResponseObject = objectArray;
                    response.Success = true;
                    break;
/*            case (byte)RespType.Null:
                return null;
            case (byte)RespType.Boolean:
                return respStringToDecode[1..] == "1";
            case (byte)RespType.Double:
                return double.Parse(respStringToDecode[1..]);
            case (byte)RespType.BigNumber:
                return decimal.Parse(respStringToDecode[1..]);
            case (byte)RespType.BulkError:
                return new Exception(respStringToDecode[1..]);
            case (byte)RespType.VerbatimString:
                return respStringToDecode[1..];
            case (byte)RespType.Map:
                return respStringToDecode[1..];
            case (byte)RespType.Set:
                return respStringToDecode[1..];
            case (byte)RespType.Push:
                return respStringToDecode[1..];
            */
                default:
                    response.Success = false;
                    response.ErrorMessage = "Invalid RESP type";
                    break;
            }
        }
        catch (Exception e)
        {
            response.Success = false;
            response.ErrorMessage = e.Message;
        }

        return response;
    }

}