using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HealthTest
{
    public class ApiClient : IApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private readonly string _apiSecret;

        public ApiClient(HttpClient httpClient, AppSettings? config = null)
        {
            _httpClient = httpClient;
            _endpoint = config?.ApiEndpoint?.TrimEnd('/') ?? string.Empty;
            _apiSecret = config?.ApiSecret ?? string.Empty;
        }

        public async Task<IPatientModel?> GetPatientFromNhsNumberAsync(string lookupValue)
        {
            string requestUri = string.IsNullOrEmpty(_endpoint) ? lookupValue : $"{_endpoint}/{lookupValue}";
            using var req = new HttpRequestMessage(HttpMethod.Get, requestUri);
            if (!string.IsNullOrEmpty(_apiSecret))
            {
                req.Headers.Add("Ocp-Apim-Subscription-Key", _apiSecret);
            }
            using var resp = await _httpClient.SendAsync(req).ConfigureAwait(false);

            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            var content = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (resp.IsSuccessStatusCode)
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<PatientModel>(content, options);
                return result;
            }

            // For any non-success (and non-404) response, throw ApiServerException containing the response body when available
            throw new ApiServerException(content);
        }
    }
}
