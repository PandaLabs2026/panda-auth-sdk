using System.Net;

namespace PandaAuth.Sdk;

public sealed class PandaAuthProtocolException : Exception
{
    public PandaAuthProtocolException(HttpStatusCode statusCode, string? error, string? errorDescription)
        : base(BuildMessage(statusCode, error, errorDescription))
    {
        StatusCode = statusCode;
        Error = error;
        ErrorDescription = errorDescription;
    }

    public HttpStatusCode StatusCode { get; }
    public string? Error { get; }
    public string? ErrorDescription { get; }

    private static string BuildMessage(HttpStatusCode statusCode, string? error, string? description)
        => $"PandaAuth protocol request failed ({(int)statusCode} {statusCode}): {error ?? "unknown_error"}"
           + (string.IsNullOrWhiteSpace(description) ? string.Empty : $" ({description})");
}
