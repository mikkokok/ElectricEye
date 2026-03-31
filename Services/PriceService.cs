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
        private readonly List<int> _allowedPricesCounts = [92, 96, 100];
        public List<ElectricityPrice> CurrentPrices { get; private set; } = [];
        public List<ElectricityPrice> TomorrowPrices { get; private set; } = [];
        public Task? CleanTask { get; private set; }
        public Task? PriceTask;

        public List<PollerStatus> GetStatus()
        {
            return _pollerUpdates;
        }

        public async Task RunPoller(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{Service}:: starting price polling", _serviceName);
            try
            {
                await InitializePrices();
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "{Service}:: initialization failed", _serviceName);
                _pollerUpdates.Add(new PollerStatus
                {
                    Time = DateTime.Now,
                    Poller = _serviceName,
                    Status = false,
                    StatusReason = $"Initialization failed, {ex.Message}"
                });
            }

            CleanTask = CleanUpdatesList(stoppingToken);
            var pollingTask = StartPolling(stoppingToken);
            try
            {
                await Task.WhenAll(CleanTask, pollingTask);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "{Service}:: all failed", _serviceName);
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
            _logger.LogInformation("{Service}:: tasks completed", _serviceName);
            _logger.LogInformation("{Service}:: ending, token: {Token}", _serviceName, stoppingToken.ToString());
        }

        private async Task StartPolling(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("{Service}:: running in the while loop, token {IsCancellationRequested} at {Now}", _serviceName, stoppingToken.IsCancellationRequested, DateTime.Now);

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
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("{Service}:: continuous polling cancelled at {Now}", _serviceName, DateTime.Now);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Service}:: polling cycle failed", _serviceName);

                    _pollerUpdates.Add(new PollerStatus
                    {
                        Time = DateTime.Now,
                        Poller = _serviceName,
                        Status = false,
                        StatusReason = $"Polling failed: {ex.Message}"
                    });

                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("{Service}:: continuous polling cancelled at {Now}", _serviceName, DateTime.Now);
                        break;
                    }
                }
            }

            _logger.LogInformation("{Service}:: exited while loop, token {IsCancellationRequested} at {Now}", _serviceName, stoppingToken.IsCancellationRequested, DateTime.Now);
        }

        private async Task UpdatePrices()
        {
            await ExecuteWithRetryAsync(UpdateTodayPrices, nameof(UpdateTodayPrices));
            await ExecuteWithRetryAsync(UpdateTomorrowPrices, nameof(UpdateTomorrowPrices));
        }

        private async Task InitializePrices()
        {
            _logger.LogInformation("{Service}:: start to initialize prices", _serviceName);

            List<ElectricityPrice> tempCurrent = await GetPricesFromFalcon();
            _logger.LogInformation("{Service}:: got {Count} current prices from Falcon", _serviceName, tempCurrent.Count);

            if (!_allowedPricesCounts.Contains(tempCurrent.Count))
            {
                await UpdateTodayPrices();
            }
            else
            {
                CurrentPrices = tempCurrent;
            }

            string tomorrowDate = DateTime.Today.AddDays(1).Date.ToString("yyyy-MM-dd").Replace(".", ":");
            var tempTomorrow = await GetPricesFromFalcon(tomorrowDate);
            _logger.LogInformation("{Service}:: got {Count} tomorrow prices from Falcon", _serviceName, tempTomorrow.Count);

            if (!_allowedPricesCounts.Contains(tempTomorrow.Count))
            {
                await UpdateTomorrowPrices();
            }
            else
            {
                TomorrowPrices = tempTomorrow;
            }

            _logger.LogInformation("{Service}:: price init completed", _serviceName);
        }

        private async Task UpdateTodayPrices()
        {
            var pricesdto = await GetTodayPrices();
            CurrentPrices = MapDTOPrices(pricesdto);
            await SendPricesToFalcon(CurrentPrices);

            _pollerUpdates.Add(new PollerStatus
            {
                Time = DateTime.Now,
                Poller = _serviceName,
                Status = true,
                StatusReason = $"Got {CurrentPrices.Count} currentprices"
            });

            _logger.LogInformation("{Service}:: today prices updated with {Count} items", _serviceName, CurrentPrices.Count);
        }

        private async Task UpdateTomorrowPrices()
        {
            var pricesdto = await GetTomorrowPrices();
            TomorrowPrices = MapDTOPrices(pricesdto);

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

            _logger.LogInformation("{Service}:: tomorrow prices updated with {Count} items", _serviceName, TomorrowPrices.Count);
        }

        private List<ElectricityPrice> MapDTOPrices(List<ElectricityPriceDTO> dtoPrices)
        {
            var pricesList = new List<ElectricityPrice>();
            foreach (var price in dtoPrices)
            {
                pricesList.Add(new ElectricityPrice
                {
                    date = price.DateTime.ToString("yyyy-MM-dd HH:mm:ss").Replace(".", ":"),
                    price = price.PriceWithTax.ToString(nfi),
                    hour = price.DateTime.Hour
                });
            }

            return pricesList;
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
                _logger.LogInformation("{Service}:: updated date to {Date}", _serviceName, _todaysDate);
            }
        }

        private async Task CleanUpdatesList(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (DateTime.Now.Day == 28 && DateTime.Now.Hour == 23)
                    {
                        _pollerUpdates.Clear();
                        _logger.LogInformation("{Service}:: cleaned updates list", _serviceName);
                    }

                    await Task.Delay(TimeSpan.FromMinutes(45), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("{Service}:: cleaning task cancelled", _serviceName);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Service}:: cleaning task error, continuing", _serviceName);

                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
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
            _logger.LogInformation("{Service}:: Fetching prices from {Url}", _serviceName, url);

            var prices = await _requestProvider.GetAsync<List<ElectricityPriceDTO>>(HttpClientConst.PricesClientName, url)
                         ?? throw new Exception($"Received null response from {url}");

            if (!_allowedPricesCounts.Contains(prices.Count))
            {
                throw new Exception($"Received {prices.Count} prices, expected one of [{string.Join(", ", _allowedPricesCounts)}]");
            }

            _logger.LogInformation("{Service}:: Successfully fetched {Count} prices from {Url}", _serviceName, prices.Count, url);
            return prices;
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

        private async Task ExecuteWithRetryAsync(Func<Task> action, string operationName)
        {
            const int maxRetries = 8;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("{Service}:: Starting operation {Operation} (attempt {Attempt}/{MaxRetries})", _serviceName, operationName, attempt, maxRetries);
                    await action();
                    _logger.LogInformation("{Service}:: Operation {Operation} succeeded on attempt {Attempt}", _serviceName, operationName, attempt);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{Service}:: Operation {Operation} attempt {Attempt}/{MaxRetries} failed: {Message}", _serviceName, operationName, attempt, maxRetries, ex.Message);

                    if (attempt == maxRetries)
                    {
                        _logger.LogError(ex, "{Service}:: Operation {Operation} failed after {MaxRetries} attempts", _serviceName, operationName, maxRetries);
                        throw;
                    }

                    var delay = TimeSpan.FromMinutes(Math.Pow(2, attempt)); // Exponential backoff: 2, 4, 8, 16, 32, 64, 128 minutes
                    _logger.LogInformation("{Service}:: Operation {Operation} waiting {DelayMinutes} minutes before retry...", _serviceName, operationName, delay.TotalMinutes);
                    await Task.Delay(delay);
                }
            }
        }
    }
}