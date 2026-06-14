using System.Net;
using BingXBot.Contracts.Dto;

namespace BingXBot.ClientApi.Http;

public sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ErrorCode { get; }
    public string? CorrelationId { get; }

    public ApiException(HttpStatusCode statusCode, string message, string? errorCode = null, string? correlationId = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        CorrelationId = correlationId;
    }

    public static ApiException From(HttpStatusCode status, ErrorResponse? err, string fallbackMessage)
    {
        var msg = err?.Detail ?? err?.Error ?? fallbackMessage;
        return new ApiException(status, msg, err?.Error, err?.CorrelationId);
    }
}
