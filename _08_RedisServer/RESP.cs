using System.Text;

namespace _08_RedisServer;

public class RESP
{
    public enum RespType
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

    private static int FindNextOccurenceOfCRLF(string respStringToDecode, int index)
    {
        var nextIndex = respStringToDecode.IndexOf("\r\n", index, StringComparison.Ordinal);
        if (nextIndex == -1)
        {
            throw new Exception("Invalid RESP type. No CRLF found");
        }

        return nextIndex;
    }

    private int _nextIndex;
    
    public RESPDecoded DecodeRESP(string respStringToDecode,int index=0)
    {
        var response = new RESPDecoded
        {
            Success = true
        };
        try
        {
            switch ((byte)respStringToDecode[index])
            {
                case (byte)RespType.SimpleString:
                case (byte)RespType.SimpleError:
                    _nextIndex=FindNextOccurenceOfCRLF(respStringToDecode, index+1);
                    response.DecodedRESPObject = respStringToDecode[(index+1).._nextIndex];
                    break;
                case (byte)RespType.Integer:
                    _nextIndex=FindNextOccurenceOfCRLF(respStringToDecode, index+1);
                    response.DecodedRESPObject = long.Parse(respStringToDecode[(index+1).._nextIndex]);
                    break;
                case (byte)RespType.BulkString:
                case (byte)RespType.BulkError:
                case (byte)RespType.VerbatimString:
                    _nextIndex=FindNextOccurenceOfCRLF(respStringToDecode, index+1);
                    var bulkStringLength = int.Parse(respStringToDecode[(index+1).._nextIndex]);
                    index = _nextIndex + 2;
                    if (bulkStringLength == -1)
                    {
                        response.DecodedRESPObject = null;
                        break;
                    }
                    response.DecodedRESPObject = respStringToDecode[index..(index+bulkStringLength)];
                    index += bulkStringLength;
                    if (respStringToDecode[index..(index+2)] != "\r\n")
                    {
                        response.Success = false;
                        response.ErrorMessage = "Invalid RESP type. No CRLF found";
                    }

                    _nextIndex = index;
                    break;
                case (byte)RespType.Array:
                    if  (index+5<=respStringToDecode.Length && respStringToDecode[index..(index+5)] == "*-1\r\n")
                    {
                        response.DecodedRESPObject = null;
                        break;
                    }
                    _nextIndex=FindNextOccurenceOfCRLF(respStringToDecode, index+1);
                    var arrayLength = int.Parse(respStringToDecode[(index+1).._nextIndex]);
                    index = _nextIndex + 2;
                    var objectArray= new object?[arrayLength];
                    for (var i = 0; i < arrayLength; i++)
                    {
                        var respDecoded = DecodeRESP(respStringToDecode, index);
                        index=_nextIndex+2;
                        if (!respDecoded.Success)
                        {
                            response.Success = false;
                            response.ErrorMessage = respDecoded.ErrorMessage;
                            return response;
                        }
                        objectArray[i]=respDecoded.DecodedRESPObject;
                    }
                    response.DecodedRESPObject = objectArray;
                    break;
            case (byte)RespType.Null:
                if (respStringToDecode[index..(index+3)] != "_\r\n")
                {
                    response.Success = false;
                    response.ErrorMessage = "Invalid RESP type";
                }
                response.DecodedRESPObject = null;
                _nextIndex=index+1;
                break;
            case (byte)RespType.Boolean:
                if (respStringToDecode[index..(index+4)] != "#t\r\n" && respStringToDecode[index..(index+4)] != "#f\r\n")
                {
                    response.Success = false;
                        response.ErrorMessage = "Invalid RESP type";
                        break;
                }
                _nextIndex=index+2;
                response.DecodedRESPObject = respStringToDecode[index+1] == 't';
                break;
            case (byte)RespType.Double:
                if (respStringToDecode.Length>=index+6 && respStringToDecode[index..(index+6)]==",nan\r\n")
                {
                    response.DecodedRESPObject = double.NaN;
                    _nextIndex=index+4;
                    break;
                }
                if (respStringToDecode.Length>=index+6 && respStringToDecode[index..(index+6)]==",inf\r\n")
                {
                    response.DecodedRESPObject = double.PositiveInfinity;
                    _nextIndex=index+4;
                    break;
                }
                if (respStringToDecode.Length>=index+7 && respStringToDecode[index..(index+7)]==",-inf\r\n")
                {
                    response.DecodedRESPObject = double.NegativeInfinity;
                    _nextIndex=index+5;
                    break;
                }
                _nextIndex=FindNextOccurenceOfCRLF(respStringToDecode, index+1);
                response.DecodedRESPObject = double.Parse(respStringToDecode[(index+1).._nextIndex]);
                break;
            case (byte)RespType.BigNumber:
                var sign=string.Empty;
                _nextIndex=FindNextOccurenceOfCRLF(respStringToDecode, index+1);
                if ((respStringToDecode[index+1]=='-')|| (respStringToDecode[index+1]=='+'))
                {
                    sign=respStringToDecode[index+1].ToString();
                    index++;
                }
                var bigNumberString = respStringToDecode[(index+1).._nextIndex];
                if (bigNumberString.Any(ch => !char.IsDigit(ch)))
                {
                    response.Success = false;
                    response.ErrorMessage = "Invalid RESP type. BigNumber must be a number";
                }
                response.DecodedRESPObject = sign+bigNumberString;
                break;
            case (byte)RespType.Map:
                response.Success = false;
                response.ErrorMessage = "Invalid RESP type. Map not yet implemented";
                break;
            case (byte)RespType.Set:
                response.Success = false;
                response.ErrorMessage = "Invalid RESP type. Set not yet implemented";
                break;
            case (byte)RespType.Push:
                response.Success = false;
                response.ErrorMessage = "Invalid RESP type. Push not yet implemented";
                break;
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

    public RESPEncoded SingleItemEncodeRESP(object? respObject, RespType type)
    {
        var response= new RESPEncoded
        {
            Success = true
        };
        var sb = new StringBuilder();
        try
        {
            switch (type)
            {
                case RespType.SimpleString:
                case RespType.SimpleError:
                    sb.Append($"{(char)type}{respObject}\r\n");
                    break;
                case RespType.Integer:
                    sb.Append($":{respObject}\r\n");
                    break;
                case RespType.BulkString:
                case RespType.BulkError:
                case RespType.VerbatimString:
                    var s = (string)respObject;
                    sb.Append($"{(char)type}{s.Length}\r\n{s}\r\n");
                    break;
                case RespType.Array:
                    response.Success = false;
                    response.ErrorMessage = "Wrong method. Use ArrayEncodeRESP";
                    break;
                case RespType.Null:
                    sb.Append("_\r\n");
                    break;
                case RespType.Boolean:
                    sb.Append((bool)respObject ? "#t\r\n" : "#f\r\n");
                    break;
                case RespType.Double:
                    if (double.IsNaN((double)respObject))
                    {
                        sb.Append(",nan\r\n");
                    }
                    else if (double.IsPositiveInfinity((double)respObject))
                    {
                        sb.Append(",inf\r\n");
                    }
                    else if (double.IsNegativeInfinity((double)respObject))
                    {
                        sb.Append(",-inf\r\n");
                    }
                    else
                    {
                        sb.Append($",{respObject}\r\n");
                    }

                    break;
                case RespType.BigNumber:
                    sb.Append($"({respObject}\r\n");
                    break;
                case RespType.Map:
                    response.Success = false;
                    response.ErrorMessage = "Map not yet implemented";
                    break;
                case RespType.Set:
                    response.Success = false;
                    response.ErrorMessage = "Set not yet implemented";
                    break;
                case RespType.Push:
                    response.Success = false;
                    response.ErrorMessage = "Push not yet implemented";
                    break;
                default:
                    response.Success = false;
                    response.ErrorMessage = $"Invalid RESP type";
                    break;
            }
        }
        catch (Exception e)
        {
            response.Success = false;
            response.ErrorMessage = $"{Enum.GetName(type)} {e.Message}";
        }
        response.EncodedRESP = sb.ToString();
        return response;
    }
    

}