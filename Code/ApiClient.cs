using System.Net;
using System.Text.Json;

namespace HealthTest;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _apiSecret;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(HttpClient httpClient, AppSettings? config = null)
    {
        _httpClient = httpClient;
        _endpoint = config?.ApiEndpoint?.TrimEnd('/') ?? string.Empty;
        _apiSecret = config?.ApiSecret ?? string.Empty;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public async Task<IPatientModel?> GetPatientFromNhsNumberAsync(string lookupValue)
    {
        var requestUri = string.IsNullOrEmpty(_endpoint) ? lookupValue : $"{_endpoint}/{lookupValue}";
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
            var result = JsonSerializer.Deserialize<PatientModel>(content, _jsonOptions);
            return result;
        }

        throw new ApiServerException("HTTP Code:" + resp.StatusCode + " " + content);
    }
}

