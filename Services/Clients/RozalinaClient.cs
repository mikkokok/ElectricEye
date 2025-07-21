using ElectricEye.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Web;
using ElectricEye.Constants;
using ElectricEye.Helpers;

namespace ElectricEye.Services.Clients
{
    public sealed class RozalinaClient(ILogger<RozalinaClient> logger, IHttpClientFactory httpClientFactory)
    {
        private readonly string _serviceName = nameof(RozalinaClient);
        private readonly ILogger<RozalinaClient> _logger = logger;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public async Task SendTelegramMessage(string from, bool admin, List<ElectricityPrice> electricityPrices)
        {
            var uriBuilder = new UriBuilder(GlobalConfig.TelegramAPIConfig!.url)
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
            var json = JsonSerializer.Serialize(GlobalConfig.TelegramAPIConfig.key);
            request.Content = new StringContent(json, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            _logger.LogInformation($"{_serviceName}:: SendTelegramMessage starting to send new data");
            var httpClient = _httpClientFactory.CreateClient(HttpClientConst.RozalinaClientName);
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation($"{_serviceName}:: SendTelegramMessage successfully sent new data");
        }
    }
}