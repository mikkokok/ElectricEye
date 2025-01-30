using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Web;
using ElectricEye.Models;

namespace ElectricEye.Helpers.Impl
{
    public class TelegramBotConsumer : ITelegramBotConsumer
    {
        private readonly IConfiguration _config;
        private string _telegramUrl;
        private string _telegramKey;
        private HttpClient _httpClient;

        public TelegramBotConsumer(IConfiguration config)
        {
            _config = config;
            InitializeTelegramBotConsumer();
        }

        private void InitializeTelegramBotConsumer()
        {
            _telegramUrl = _config["TelegramAPI:url"];
            _telegramKey = _config["TelegramAPI:key"];
            _httpClient = new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 30)
            };
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
            try
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
        }
    }
}
