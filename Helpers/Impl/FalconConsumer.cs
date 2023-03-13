using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Web;
using ElectricEye.Models;

namespace ElectricEye.Helpers.Impl
{
    public class FalconConsumer : IFalconConsumer
    {
        private readonly IConfiguration _config;
        private string _falconUrl;
        private string _falconKey;
        private HttpClient _httpClient;
        private CertificateValidator _certificateValidator;

        public FalconConsumer(IConfiguration config)
        {
            _config = config;
            InitializeFalconConsumer();
        }

        private void InitializeFalconConsumer()
        {
            _falconUrl = _config["RestlessFalcon:url"];
            _falconKey = _config["RestlessFalcon:key"];
            _certificateValidator = new CertificateValidator(_config);
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = _certificateValidator.ValidateCertificate
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = new TimeSpan(0, 0, 30)
            };
        }

        public async Task<List<ElectricityPrice>> GetCurrentElectricityPrices(int ago = 0, DateTime date = new DateTime())
        {
            var uriBuilder = new UriBuilder(_falconUrl)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = 443
            };
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            if (ago != 0)
            {
                query["ago"] = ago.ToString();
            }
            if (date.Year != 0001)
            {
                query["date"] = date.ToString();
            }
            using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                var prices = JsonSerializer.Deserialize<List<ElectricityPrice>>(responseContent);
                return prices == null ? throw new Exception("Something funky happened") : prices;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
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

            using var request = new HttpRequestMessage(HttpMethod.Post, uriBuilder.Uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var json = JsonSerializer.Serialize(prices);
            request.Content = new StringContent(json, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            try
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
