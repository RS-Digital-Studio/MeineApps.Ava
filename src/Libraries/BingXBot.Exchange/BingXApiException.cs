namespace BingXBot.Exchange;

public class BingXApiException : Exception
{
    public int ErrorCode { get; }
    public string ErrorMessage { get; }

    public BingXApiException(int code, string message)
        : base($"BingX API Error {code}: {message}")
    {
        ErrorCode = code;
        ErrorMessage = message;
    }
}
