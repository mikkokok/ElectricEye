using ElectricEye.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using ElectricEye.Extensions;

namespace ElectricEye.Services.Clients
{
    public sealed class ChargerClient
    {
        private string _serviceName;
        private ILogger<ChargerClient> _logger;
        private IConfiguration _config;
        private string _chargerUrl;
        private readonly IHttpClientFactory _httpClientFactory;

        public ChargerClient(IConfiguration config, ILogger<ChargerClient> logger, IHttpClientFactory httpClientFactory)
        {
            _serviceName = nameof(ChargerClient);
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _config = config;
            _chargerUrl = _config["ChargerUrl"] ?? throw new Exception("Charger URL not found from configs");
        }

        public async Task<ChargerDTO> GetLatestConsumption()
        {
            _logger.LogInformation($"{_serviceName}:: GetLatestConsumption start to get latest consumption");
            var uriBuilder = new UriBuilder(_chargerUrl)
            {
                Scheme = Uri.UriSchemeHttp,
                Port = 80
            };
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);

            uriBuilder.Query = query.ToString();
            using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var httpClient = _httpClientFactory.CreateClient(HttpClientConst.DefaultClientName);
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var reading = JsonSerializer.Deserialize<ChargerDTO>(responseContent);
            _logger.LogInformation($"{_serviceName}:: GetLatestConsumption got {reading}");
            return reading ?? throw new Exception($"Could not get reasonable values from {_chargerUrl}, reading was null");
        }
    }
}
