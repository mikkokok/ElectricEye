using ElectricEye.Models;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Web;

namespace ElectricEye.Services.Clients
{
    public sealed class RozalinaClient
    {
        private readonly string _serviceName = "";
        private readonly ILogger<RozalinaClient> _logger;
        private readonly IConfiguration _config;
        private readonly string _telegramUrl;
        private readonly string _telegramKey;
        private HttpClient? _httpClient;

        public RozalinaClient(IConfiguration config, ILogger<RozalinaClient> logger)
        {
            _serviceName = nameof(RozalinaClient);
            _logger = logger;
            _config = config;
            _telegramUrl = _config["TelegramAPI:url"] ?? throw new Exception($"{_serviceName} initialisation failed due to TelegramAPI:url config being null");
            _telegramKey = _config["TelegramAPI:key"] ?? throw new Exception($"{_serviceName} initialisation failed due to TelegramAPI:key config being null");
        }
        private HttpClient GetHttpClient()
        {
            _httpClient ??= new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 30)
            };
            return _httpClient;
        }

        public async Task SendTelegramMessage(string from, bool admin, List<ElectricityPrice> electricityPrices)
        {
            var uriBuilder = new UriBuilder(_telegramUrl)
            {
                Scheme = Uri.UriSchemeHttp,
                Port = 84
            };
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            StringBuilder sb = new();
            foreach (var price in electricityPrices)
            {
                sb.Append(price.date);
                sb.Append(' ');
                sb.Append(price.price);
                sb.AppendLine(" ");
            }
            query["message"] = sb.ToString();
            query["from"] = from;
            query["admin"] = admin.ToString();
            uriBuilder.Query = query.ToString() ?? throw new Exception("Empty URL built");

            using var request = new HttpRequestMessage(HttpMethod.Post, uriBuilder.Uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var json = JsonSerializer.Serialize(_telegramKey);
            request.Content = new StringContent(json, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            _logger.LogInformation($"{_serviceName}:: SendTelegramMessage starting to send new data");
            var httpClient = GetHttpClient();
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation($"{_serviceName}:: SendTelegramMessage successfully sent new data");
        }
    }
}
