using ElectricEye.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ElectricEye.Helpers.Impl
{
    public class ApiPoller : IApiPoller
    {
        private readonly IConfiguration _config;
        public bool IsRunning { get; set; }
        private readonly int _desiredPollingHour = 3;
        private readonly HttpClient _httpClient;
        public List<ElectricityPrice> CurrentPrices { get; private set; }
        public ApiPoller(IConfiguration config)
        {
            _config = config;
            IsRunning = true;
            _httpClient = new HttpClient();
            CurrentPrices = new List<ElectricityPrice>();
            UpdatePrices();
        }

        private async Task StartPolling()
        {
            while (IsRunning)
            {
                if (_desiredPollingHour == DateTime.Now.Hour)
                {
                    await UpdatePrices();
                }
                Thread.Sleep(TimeSpan.FromMinutes(30));
            }
            IsRunning = false;
        }

        private async Task UpdatePrices()
        {
            if (CurrentPrices.Count == 0)
            {
                _ = CollectPrices(_config["TodaySpotAPI"]);
            } 
        }

        private async Task<List<ElectricityPriceDTO>?> CollectPrices(string url)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                var prices = JsonSerializer.Deserialize<List<ElectricityPriceDTO>>(responseContent);
                return prices;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
