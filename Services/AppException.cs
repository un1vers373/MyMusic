namespace MyMusic.Services;

/// <summary>An expected error that is returned to the client as JSON { "error": "..." }.</summary>
public class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
