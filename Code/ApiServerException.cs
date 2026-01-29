using System.Net;

namespace HealthTest;

public class ApiServerException : Exception
{
    public string ResponseContent { get; }
    public HttpStatusCode StatusCode { get; }

    public ApiServerException(HttpStatusCode statusCode, string responseContent)
        : base(string.IsNullOrWhiteSpace(responseContent)
                ? $"API server returned an error: {statusCode}"
                : $"API server returned an error: {statusCode} {responseContent}")
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }
}

