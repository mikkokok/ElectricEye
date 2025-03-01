using ElectricEye.Models;
using System.Text.Json;

namespace ElectricEye.Services.Clients
{
    public sealed class PricesClient
    {
        private readonly string _serviceName;
        private readonly ILogger<PricesClient> _logger;
        private readonly IConfiguration _config;
        private readonly string _todaySpotUrl;
        private readonly string _tomorrowSpotUrl;
        private HttpClient? _httpClient;

        public PricesClient(IConfiguration config, ILogger<PricesClient> logger)
        {
            _serviceName = nameof(PricesClient);
            _logger = logger;
            _config = config;
            _todaySpotUrl = _config["TodaySpotAPI"] ?? throw new Exception($"{_serviceName} initialisation failed due to TodaySpotAPI config being null");
            _tomorrowSpotUrl = _config["TomorrowSpotAPI"] ?? throw new Exception($"{_serviceName} initialisation failed due to TodaySpotAPI config being null");
        }

        private HttpClient GetHttpClient()
        {
            _httpClient ??= new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 30)
            };
            return _httpClient;
        }

        private async Task<List<ElectricityPriceDTO>> CollectPrices(string url)
        {
            _logger.LogInformation($"{_serviceName}:: CollectPrices start to get prices from url {url}");
            var httpClient = GetHttpClient();
            HttpResponseMessage response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation($"{_serviceName}:: CollectPrices got response {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            var prices = JsonSerializer.Deserialize<List<ElectricityPriceDTO>>(responseContent);
            return prices ?? throw new Exception($"{_serviceName} got null as prices");
        }

        public async Task<List<ElectricityPriceDTO>> CollectTodayPrices()
        {
            return await CollectPrices(_todaySpotUrl);
        }

        public async Task<List<ElectricityPriceDTO>> CollectTomorrowPrices()
        {
            return await CollectPrices(_tomorrowSpotUrl);
        }
    }
}
