using ElectricEye.Helpers;
using ElectricEye.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;

namespace ElectricEye.Services.Clients
{
    public sealed class FalconClient
    {
        private readonly string _serviceName = "";
        private readonly ILogger<FalconClient> _logger;
        private readonly IConfiguration _config;
        private string _falconUrl;
        private string _falconKey;
        private string _falconChargerUrl;
        private readonly IHttpClientFactory _httpClientFactory;


        public FalconClient(IConfiguration config, ILogger<FalconClient> logger, IHttpClientFactory httpClientFactory)
        {
            _serviceName = nameof(FalconClient);
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _config = config;
            _falconUrl = _config["RestlessFalcon:url"] ?? throw new Exception($"{_serviceName} initialisation failed due to RestlessFalcon:url config being null");
            _falconKey = _config["RestlessFalcon:key"] ?? throw new Exception($"{_serviceName} initialisation failed due to RestlessFalcon:key config being null");
            _falconChargerUrl = _config["RestlessFalcon:chargingUrl"] ?? throw new Exception($"{_serviceName} initialisation failed due to RestlessFalcon:chargingUrl config being null");
        }

        public async Task<List<ElectricityPrice>> GetElectricityPrices(int ago = 0, string date = "")
        {
            var uriBuilder = new UriBuilder(_falconUrl)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = 443
            };
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            if (!string.IsNullOrEmpty(date))
            {
                query["date"] = date;
            }
            else if (string.IsNullOrEmpty(date))
            {
                query["ago"] = ago.ToString();
            }

            uriBuilder.Query = query.ToString();
            using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var httpClient = _httpClientFactory.CreateClient(nameof(FalconClient));
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var prices = JsonSerializer.Deserialize<List<ElectricityPrice>>(responseContent);
            _logger.LogInformation($"{_serviceName}:: GetElectricityPrices got {prices?.Count} as response");
            return prices ?? throw new Exception($"{_serviceName} got null as electricity prices");
        }

        public async Task SendElectricityPrices(List<ElectricityPrice> prices)
        {
            var uriBuilder = new UriBuilder(_falconUrl)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = 443
            };
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["authKey"] = _falconKey;
            uriBuilder.Query = query.ToString() ?? throw new Exception("Empty URL built");

            using HttpRequestMessage request = new(HttpMethod.Post, uriBuilder.Uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var json = JsonSerializer.Serialize(prices);
            request.Content = new StringContent(json, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var httpClient = _httpClientFactory.CreateClient(nameof(FalconClient));
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation($"{_serviceName}:: SendElectricityPrices successfully sent new prices");
        }

        public async Task SendChargingData(CarCharge charge)
        {
            var uriBuilder = new UriBuilder(_falconChargerUrl)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = 443
            };
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["authKey"] = _falconKey;
            uriBuilder.Query = query.ToString() ?? throw new Exception("Empty URL built");

            using var request = new HttpRequestMessage(HttpMethod.Post, uriBuilder.Uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var json = JsonSerializer.Serialize(charge);
            request.Content = new StringContent(json, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var httpClient = _httpClientFactory.CreateClient(nameof(FalconClient));
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation($"{_serviceName}:: SendChargingData successfully sent new data");
        }
    }
}
