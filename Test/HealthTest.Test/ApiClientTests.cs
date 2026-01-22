using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HealthTest;
using Xunit;

namespace HealthTest.Test
{
    public class ApiClientTests
    {
        private class FakeHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            public FakeHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }

        private class CaptureHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;
            public HttpRequestMessage? LastRequest { get; private set; }

            public CaptureHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(_response);
            }
        }

        [Fact]
        public async Task GetPatientFromNhsNumberAsync_ReturnsNull_On404()
        {
            var response = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };
            var client = new HttpClient(new FakeHandler(response));
            var api = new ApiClient(client, new AppSettings { ApiEndpoint = "http://example" });

            var result = await api.GetPatientFromNhsNumberAsync("123");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetPatientFromNhsNumberAsync_ThrowsApiServerException_On500()
        {
            var body = "server error details";
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(body) };
            var client = new HttpClient(new FakeHandler(response));
            var api = new ApiClient(client, new AppSettings { ApiEndpoint = "http://example" });

            var ex = await Assert.ThrowsAsync<ApiServerException>(async () => await api.GetPatientFromNhsNumberAsync("123"));
            Assert.Contains("server error details", ex.Message);
            Assert.Equal(body, ex.ResponseContent);
        }

        [Fact]
        public async Task GetPatientFromNhsNumberAsync_ParsesJson_On200()
        {
            var json = "{ \"nhsNumber\": \"123\", \"name\": \"Alice\", \"born\": \"01-02-2020\" }";
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            var client = new HttpClient(new FakeHandler(response));
            var api = new ApiClient(client, new AppSettings { ApiEndpoint = "http://example" });

            var result = await api.GetPatientFromNhsNumberAsync("123");

            Assert.NotNull(result);
            Assert.Equal("123", result!.nhsNumber);
            Assert.Equal("Alice", result.name);
            Assert.Equal("01-02-2020", result.born);
        }

        [Fact]
        public async Task GetPatientFromNhsNumberAsync_AddsSubscriptionKeyHeader_FromConfig()
        {
            var json = "{ \"nhsNumber\": \"123\", \"name\": \"Alice\", \"born\": \"01-02-2020\" }";
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            var handler = new CaptureHandler(response);
            var client = new HttpClient(handler);
            var api = new ApiClient(client, new AppSettings { ApiEndpoint = "http://example", ApiSecret = "secret-value" });

            await api.GetPatientFromNhsNumberAsync("123");

            Assert.NotNull(handler.LastRequest);
            Assert.Equal("http://example/123", handler.LastRequest.RequestUri.ToString()); // Verify URL construction
            Assert.True(handler.LastRequest.Headers.Contains("Ocp-Apim-Subscription-Key"));
            var values = handler.LastRequest.Headers.GetValues("Ocp-Apim-Subscription-Key");
            Assert.Contains("secret-value", values);
        }
    }
}
