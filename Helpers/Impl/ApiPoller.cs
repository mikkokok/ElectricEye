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
        private readonly int _desiredPollingHour = 15;
        private readonly HttpClient _httpClient;
        private readonly NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;
        public List<ElectricityPrice> CurrentPrices { get; private set; }
        public List<ElectricityPrice> TomorrowPrices { get; private set; }

        private readonly FalconConsumer _falconConsumer;
        private readonly TelegramBotConsumer _telegramConsumer;
        private DateTime _todaysDate;
        private bool _pricesSent = false;

        public ApiPoller(IConfiguration config, FalconConsumer falconConsumer, TelegramBotConsumer telegramConsumer)
        {
            _config = config;
            IsRunning = true;
            _httpClient = new HttpClient();
            CurrentPrices = new List<ElectricityPrice>();
            TomorrowPrices = new List<ElectricityPrice>();
            _falconConsumer = falconConsumer;
            _telegramConsumer = telegramConsumer;
            _ = InitializePrices();
            Task.Run(() => StartPolling());
        }

        private async Task StartPolling()
        {
            while (IsRunning)
            {
                UpdateToday();
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
            List<ElectricityPrice> tempCurrent = await _falconConsumer.GetElectricityPrices(0);
            if (tempCurrent.Count == 0)
            {
                await UpdateTodayPrices();
            }

            string tomorrowDate = DateTime.Today.AddDays(1).Date.ToString("yyyy-MM-dd").Replace(".", ":");
            var tempTomorrow = await _falconConsumer.GetElectricityPrices(0, tomorrowDate);
            if (tempTomorrow.Count == 0) {
                await UpdateTomorrowPrices();
            }
        }

        private async Task UpdateTodayPrices()
        {
            var pricesdto = await CollectPrices(_config["TodaySpotAPI"]);
            CurrentPrices = MapDTOPrices(pricesdto!);
            _ = _falconConsumer.SendElectricityPrices(CurrentPrices);
        }

        private async Task UpdateTomorrowPrices()
        {
            try
            {
                var pricesdto = await CollectPrices(_config["TomorrowSpotAPI"]);
                TomorrowPrices = MapDTOPrices(pricesdto!);
                if (!_pricesSent)
                {
                    CheckForHighPrice(TomorrowPrices);
                    _pricesSent = true;
                }
                
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
                    date = price.DateTime.ToString("yyyy-MM-dd HH:mm:ss").Replace(".", ":"),
                    price = price.PriceWithTax.ToString(nfi),
                    hour = price.DateTime.Hour
                });
            }
            return PricesList;
        }
        private void CheckForHighPrice(List<ElectricityPrice> prices)
        {
            foreach(var price in prices)
            {
                _ = double.TryParse(price.price, out double result);
                if ( result > 0.1)
                {
                    _ = _telegramConsumer.SendTelegramMessage("ElectricEye", true, prices);
                    break;
                }
            }

        }

        private void UpdateToday()
        {
            if (_todaysDate != DateTime.Today.Date)
            {
                _todaysDate = DateTime.Today.Date;
                _pricesSent = false;
            }

        }
    }
}
