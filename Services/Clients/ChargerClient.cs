using ElectricEye.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using ElectricEye.Extensions;
using ElectricEye.Constants;
using ElectricEye.Helpers;

namespace ElectricEye.Services.Clients
{
    public sealed class ChargerClient(ILogger<ChargerClient> logger, IHttpClientFactory httpClientFactory)
    {
        private string _serviceName = nameof(ChargerClient);
        private readonly ILogger<ChargerClient> _logger = logger;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public async Task<ChargerDTO> GetLatestConsumption()
        {
            _logger.LogInformation($"{_serviceName}:: GetLatestConsumption start to get latest consumption");
            var uriBuilder = new UriBuilder(GlobalConfig.ChargerUrl!)
            {
                Scheme = Uri.UriSchemeHttp,
                Port = 80
            };
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);

            uriBuilder.Query = query.ToString();
            using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var httpClient = _httpClientFactory.CreateClient(HttpClientConst.ChargerClientName);
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var reading = JsonSerializer.Deserialize<ChargerDTO>(responseContent);
            _logger.LogInformation($"{_serviceName}:: GetLatestConsumption got {reading}");
            return reading ?? throw new Exception($"Could not get reasonable values from {uriBuilder.Uri}, reading was null");
        }
    }
}
