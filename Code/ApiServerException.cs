using System;

namespace HealthTest
{
    public class ApiServerException : Exception
    {
        public string ResponseContent { get; }

        public ApiServerException(string responseContent)
            : base(string.IsNullOrWhiteSpace(responseContent)
                  ? "API server returned an error"
                  : $"API server returned an error: {responseContent}")
        {
            ResponseContent = responseContent;
        }
    }
}
