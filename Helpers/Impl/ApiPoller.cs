using ElectricEye.Models;
using System.Globalization;
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
        private readonly NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;
        public List<ElectricityPrice> CurrentPrices { get; private set; }
        public List<ElectricityPrice> TomorrowPrices { get; private set; }

        private readonly FalconConsumer _falconConsumer;

        public ApiPoller(IConfiguration config, FalconConsumer falconConsumer)
        {
            _config = config;
            IsRunning = true;
            _httpClient = new HttpClient();
            CurrentPrices = new List<ElectricityPrice>();
            TomorrowPrices = new List<ElectricityPrice>();
            _falconConsumer = falconConsumer;
            _ = InitializePrices();
            Task.Run(() => StartPolling());
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
            await UpdateTodayPrices();
            await UpdateTomorrowPrices();
        }
        private async Task InitializePrices()
        {
            var tempCurrent = await _falconConsumer.GetElectricityPrices(0);
            if (tempCurrent.Count == 0)
            {
                await UpdateTodayPrices();
            }

            var tempTomorrow = await _falconConsumer.GetElectricityPrices(0, DateTime.Today.AddDays(1).Date.ToString("yyyy-MM-dd").Replace(".", ":"));
            if (tempTomorrow.Count == 0) { 
                await UpdateTomorrowPrices();
            }
        }

        private async Task UpdateTodayPrices()
        {
            var pricesdto = await CollectPrices(_config["TodaySpotAPI"]);
            CurrentPrices = MapDTOPrices(pricesdto);
            _ = _falconConsumer.SendElectricityPrices(CurrentPrices);
        }

        private async Task UpdateTomorrowPrices()
        {
            try
            {
                var pricesdto = await CollectPrices(_config["TomorrowSpotAPI"]);
                TomorrowPrices = MapDTOPrices(pricesdto);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
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

        private List<ElectricityPrice> MapDTOPrices(List<ElectricityPriceDTO> DTOPRices)
        {
            var PricesList = new List<ElectricityPrice>();
            foreach (var price in DTOPRices)
            {
                PricesList.Add(new ElectricityPrice
                {
                    Date = price.DateTime.Date.ToString("yyyy-MM-dd").Replace(".", ":"),
                    Price = price.PriceWithTax.ToString(nfi),
                    Hour = price.DateTime.Hour
                });
            }
            return PricesList;
        }
    }
}
