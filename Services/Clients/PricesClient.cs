using ElectricEye.Models;
using System.Text.Json;
using ElectricEye.Extensions;
using ElectricEye.Constants;
using ElectricEye.Helpers;

namespace ElectricEye.Services.Clients
{
    public sealed class PricesClient(ILogger<PricesClient> logger, IHttpClientFactory httpClientFactory)
    {
        private readonly string _serviceName = nameof(PricesClient);
        private readonly ILogger<PricesClient> _logger = logger;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        private async Task<List<ElectricityPriceDTO>> CollectPrices(string url)
        {
            _logger.LogInformation($"{_serviceName}:: CollectPrices start to get prices from url {url}");
            var httpClient = _httpClientFactory.CreateClient(HttpClientConst.PricesClientName);
            HttpResponseMessage response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation($"{_serviceName}:: CollectPrices got response {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            var prices = JsonSerializer.Deserialize<List<ElectricityPriceDTO>>(responseContent);
            return prices ?? throw new Exception($"{_serviceName} got null as prices");
        }

        public async Task<List<ElectricityPriceDTO>> CollectTodayPrices()
        {
            return await CollectPrices(GlobalConfig.PricesAPIConfig!.baseUrl + GlobalConfig.PricesAPIConfig.todaySpotAPI);
        }

        public async Task<List<ElectricityPriceDTO>> CollectTomorrowPrices()
        {
            return await CollectPrices(GlobalConfig.PricesAPIConfig!.baseUrl + GlobalConfig.PricesAPIConfig.tomorrowSpotAPI);
        }
    }
}
