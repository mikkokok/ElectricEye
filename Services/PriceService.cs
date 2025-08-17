using ElectricEye.Constants;
using ElectricEye.Helpers;
using ElectricEye.Models;
using System.Globalization;
using System.Text;
using System.Web;

namespace ElectricEye.Services
{
    public sealed class PriceService(ILogger<PriceService> logger, IRequestProvider requestProvider)
    {
        private readonly string _serviceName = nameof(PriceService);
        private readonly ILogger<PriceService> _logger = logger;
        private readonly IRequestProvider _requestProvider = requestProvider;
        private readonly NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;
        private readonly List<PollerStatus> _pollerUpdates = [];
        private DateTime _todaysDate;
        private bool _pricesSent = true;
        private readonly int _desiredPollingHour = 14;
        public List<ElectricityPrice> CurrentPrices { get; private set; } = [];
        public List<ElectricityPrice> TomorrowPrices { get; private set; } = [];

        public List<PollerStatus> GetStatus()
        {
            return _pollerUpdates;
        }
        public async Task RunPoller(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{_serviceName}:: starting price polling");
            try
            {
                await InitializePrices();
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{_serviceName}:: initialization failed", ex.Message);
                _pollerUpdates.Add(new PollerStatus
                {
                    Time = DateTime.Now,
                    Poller = _serviceName,
                    Status = false,
                    StatusReason = $"Initialization failed, {ex.Message}"
                });

            }

            var CleaningTask = CleanUpdatesList();
            var PollingTask = StartPolling(stoppingToken);
            try
            {
                await Task.WhenAll(CleaningTask, PollingTask);
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{_serviceName}:: all failed", ex.Message);
                _pollerUpdates.Add(new PollerStatus
                {
                    Time = DateTime.Now,
                    Poller = _serviceName,
                    Status = false,
                    StatusReason = $"All failed, {ex.Message}"
                });
            }

            _pollerUpdates.Add(new PollerStatus
            {
                Time = DateTime.Now,
                Poller = _serviceName,
                Status = false,
                StatusReason = "Tasks completed"
            });
            _logger.LogInformation($"{_serviceName}:: tasks completed");
            _logger.LogInformation($"{_serviceName}:: ending", stoppingToken.ToString());
        }

        private async Task StartPolling(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation($"{_serviceName}:: running in the while loop, token {stoppingToken.IsCancellationRequested}", DateTime.Now);
                _pollerUpdates.Add(new PollerStatus
                {
                    Time = DateTime.Now,
                    Poller = _serviceName,
                    Status = true,
                    StatusReason = "Running in the while loop"
                });
                try
                {
                    if (_desiredPollingHour == DateTime.Now.Hour)
                    {
                        UpdateToday();
                        if (_pricesSent == false)
                        {
                            await UpdatePrices();
                        }
                        _pricesSent = true;
                    }
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"{_serviceName} update failed", ex.ToString());
                    _pollerUpdates.Add(new PollerStatus
                    {
                        Time = DateTime.Now,
                        Poller = _serviceName,
                        Status = false,
                        StatusReason = ex.Message ?? ex.StackTrace ?? ex.ToString()
                    });
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                }
            }
            _logger.LogInformation($"{_serviceName}:: exited while loop, token {stoppingToken.IsCancellationRequested}", DateTime.Now);
        }

        private async Task UpdatePrices()
        {
            await UpdateTodayPrices();
            await UpdateTomorrowPrices();
        }
        private async Task InitializePrices()
        {
            _logger.LogInformation($"{_serviceName}:: start to initialize prices");
            List<ElectricityPrice> tempCurrent = await GetPricesFromFalcon();
            if (tempCurrent.Count == 0)
            {
                await UpdateTodayPrices();
            }
            else
            {
                CurrentPrices = tempCurrent;
            }

            string tomorrowDate = DateTime.Today.AddDays(1).Date.ToString("yyyy-MM-dd").Replace(".", ":");
            var tempTomorrow = await GetPricesFromFalcon(tomorrowDate);
            if (tempTomorrow.Count == 0)
            {
                await UpdateTomorrowPrices();
            }
            else
            {
                TomorrowPrices = tempTomorrow;
            }
            _logger.LogInformation($"{_serviceName}:: price init completed");
        }

        private async Task UpdateTodayPrices()
        {
            var pricesdto = await GetTodayPrices(); ;
            CurrentPrices = MapDTOPrices(pricesdto);
            await SendPricesToFalcon(CurrentPrices);
            _pollerUpdates.Add(new PollerStatus
            {
                Time = DateTime.Now,
                Poller = _serviceName,
                Status = true,
                StatusReason = $"Got {CurrentPrices.Count} currentprices"
            });
            _logger.LogInformation($"{_serviceName}:: today prices updated with {CurrentPrices.Count} amount");
        }

        private async Task UpdateTomorrowPrices()
        {
            var pricesdto = await GetTomorrowPrices();
            TomorrowPrices = MapDTOPrices(pricesdto!);
            if (!_pricesSent)
            {
                await CheckForHighPriceAsync(TomorrowPrices);
                _pricesSent = true;
            }
            await SendPricesToFalcon(TomorrowPrices);
            _pollerUpdates.Add(new PollerStatus
            {
                Time = DateTime.Now,
                Poller = _serviceName,
                Status = true,
                StatusReason = $"Got {TomorrowPrices.Count} tomorrowprices"
            });
            _logger.LogInformation($"{_serviceName}:: tomorrow prices updated with {TomorrowPrices.Count} amount");
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
        private async Task CheckForHighPriceAsync(List<ElectricityPrice> prices)
        {
            foreach (var price in prices)
            {
                _ = double.TryParse(price.price, out double result);
                if (result > 0.1)
                {
                    await SendTelegramMessage("ElectricEye", true, prices);
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
                _logger.LogInformation($"{_serviceName}:: updated date to {_todaysDate}");
            }
        }
        private async Task CleanUpdatesList()
        {
            while (true)
            {
                try
                {
                    if (DateTime.Now.Day == 28 && DateTime.Now.Hour == 23)
                    {
                        _pollerUpdates.Clear();
                        _logger.LogInformation($"{_serviceName}:: cleaned updates list");
                    }
                    await Task.Delay(TimeSpan.FromMinutes(45));
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"{_serviceName}:: cleaning updates list failed", ex.Message);
                }
            }
        }

        private async Task<List<ElectricityPriceDTO>> GetTodayPrices()
        {
            return await GetPrices(GlobalConfig.PricesAPIConfig!.baseUrl + GlobalConfig.PricesAPIConfig.todaySpotAPI);

        }
        private async Task<List<ElectricityPriceDTO>> GetTomorrowPrices()
        {
            return await GetPrices(GlobalConfig.PricesAPIConfig!.baseUrl + GlobalConfig.PricesAPIConfig.tomorrowSpotAPI);
        }
        private async Task<List<ElectricityPriceDTO>> GetPrices(string url)
        {
            var prices = await _requestProvider.GetAsync<List<ElectricityPriceDTO>>(HttpClientConst.PricesClientName, url);
            return prices ?? throw new Exception($"Getting latest readings from {url} failed");
        }
        private async Task SendTelegramMessage(string sender, bool sendToAdmin, List<ElectricityPrice> electricityPrices)
        {
            StringBuilder sb = new();
            foreach (var price in electricityPrices)
            {
                sb.Append(price.date);
                sb.Append(' ');
                sb.Append(price.price);
                sb.AppendLine(" ");
            }
            var query = HttpUtility.ParseQueryString(new UriBuilder().Query);
            query["message"] = sb.ToString();
            query["from"] = sender;
            query["admin"] = sendToAdmin.ToString();
            var url = string.Join("?", GlobalConfig.TelegramAPIConfig!.url, query.ToString());
            await _requestProvider.PostAsync(HttpClientConst.RozalinaClientName, url, GlobalConfig.TelegramAPIConfig.key);
        }

        private async Task<List<ElectricityPrice>> GetPricesFromFalcon(string date = "")
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            if (!string.IsNullOrEmpty(date))
            {
                query["date"] = date;
            }
            else
            {
                query["ago"] = "0";
            }
            var url = string.Join("?", GlobalConfig.RestlessFalconConfig!.baseUrl + GlobalConfig.RestlessFalconConfig.electricityPriceUrl, query.ToString());
            var prices = await _requestProvider.GetAsync<List<ElectricityPrice>>(HttpClientConst.FalconClientName, url);
            return prices ?? throw new Exception($"Getting prices from {url} failed");
        }

        private async Task SendPricesToFalcon(List<ElectricityPrice> prices)
        {
            var url = GlobalConfig.RestlessFalconConfig!.baseUrl + GlobalConfig.RestlessFalconConfig.electricityPriceUrl;
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["authKey"] = GlobalConfig.RestlessFalconConfig.key;
            url = string.Join("?", url, query.ToString());
            await _requestProvider.PostAsync(HttpClientConst.FalconClientName, url, prices);
        }
    }
}
