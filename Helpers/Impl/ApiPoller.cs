using ElectricEye.Models;
using System.Globalization;
using System.Text.Json;

namespace ElectricEye.Helpers.Impl
{
    public class ApiPoller : BackgroundService, IApiPoller
    {
        private readonly IConfiguration _config;
        public bool IsRunning { get; set; }
        private readonly int _desiredPollingHour = 15;
        private readonly HttpClient _httpClient;
        private const string _pollerName = "ApiPoller";
        private readonly NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;
        public List<ElectricityPrice> CurrentPrices { get; private set; }
        public List<ElectricityPrice> TomorrowPrices { get; private set; }
        private readonly FalconConsumer _falconConsumer;
        private readonly TelegramBotConsumer _telegramConsumer;
        private DateTime _todaysDate;
        private bool _pricesSent = true;
        private static List<PollerStatus> _pollerUpdates = new();


        public ApiPoller(IConfiguration config)
        {
            _config = config;
            IsRunning = true;
            _httpClient = new HttpClient();
            CurrentPrices = new List<ElectricityPrice>();
            TomorrowPrices = new List<ElectricityPrice>();
            _falconConsumer = new FalconConsumer(_config);
            _telegramConsumer = new TelegramBotConsumer(_config);
        }

        public List<PollerStatus> GetStatus()
        {
            return _pollerUpdates;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            try
            {
                await InitializePrices();
            }
            catch (Exception ex)
            {
                _pollerUpdates.Add(new PollerStatus
                {
                    Time = DateTime.Now,
                    Poller = _pollerName,
                    Status = false,
                    StatusReason = $"Initialization failed, {ex.Message}"
                });
            }

            var CleaningTask = CleanUpdatesList();
            var PollingTask = StartPolling();
            await Task.WhenAll(CleaningTask, PollingTask);
        }

        private async Task StartPolling()
        {
            while (IsRunning)
            {
                _pollerUpdates.Add(new PollerStatus
                {
                    Time = DateTime.Now,
                    Poller = _pollerName,
                    Status = true,
                    StatusReason = "Running in the while loop"
                });
                try
                {
                    if (_desiredPollingHour == DateTime.Now.Hour)
                    {
                        UpdateToday();
                        await UpdatePrices();
                    }
                    await Task.Delay(TimeSpan.FromMinutes(30));
                }
                catch (Exception ex)
                {
                    _pollerUpdates.Add(new PollerStatus
                    {
                        Time = DateTime.Now,
                        Poller = _pollerName,
                        Status = false,
                        StatusReason = ex.Message
                    });
                }
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
            if (tempTomorrow.Count == 0)
            {
                await UpdateTomorrowPrices();
            }
        }

        private async Task UpdateTodayPrices()
        {
            var pricesdto = await CollectPrices(_config["TodaySpotAPI"]);
            CurrentPrices = MapDTOPrices(pricesdto!);
            await _falconConsumer.SendElectricityPrices(CurrentPrices);
            _pollerUpdates.Add(new PollerStatus
            {
                Time = DateTime.Now,
                Poller = _pollerName,
                Status = true,
                StatusReason = $"Got {CurrentPrices.Count} currentprices"
            });
        }

        private async Task UpdateTomorrowPrices()
        {
            var pricesdto = await CollectPrices(_config["TomorrowSpotAPI"]);
            TomorrowPrices = MapDTOPrices(pricesdto!);
            if (!_pricesSent)
            {
                CheckForHighPrice(TomorrowPrices);
                _pricesSent = true;
            }
            await _falconConsumer.SendElectricityPrices(TomorrowPrices);
            _pollerUpdates.Add(new PollerStatus
            {
                Time = DateTime.Now,
                Poller = _pollerName,
                Status = true,
                StatusReason = $"Got {TomorrowPrices.Count} tomorrowprices"
            });
        }


        private async Task<List<ElectricityPriceDTO>?> CollectPrices(string url)
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var prices = JsonSerializer.Deserialize<List<ElectricityPriceDTO>>(responseContent);
            return prices;
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
            foreach (var price in prices)
            {
                _ = double.TryParse(price.price, out double result);
                if (result > 0.1)
                {
                    var task = Task.Run(async () => await _telegramConsumer.SendTelegramMessage("ElectricEye", true, prices));
                    try
                    {
                        task.Wait();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        throw;
                    }
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

        private async Task CleanUpdatesList()
        {
            while (true)
            {
                if (DateTime.Now.Day == 28 && DateTime.Now.Hour == 23)
                {
                    _pollerUpdates.Clear();
                }
                await Task.Delay(TimeSpan.FromMinutes(45));
            }
        }
    }
}