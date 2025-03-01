using ElectricEye.Models;
using ElectricEye.Services.Clients;
using System.Globalization;

namespace ElectricEye.Services
{
    public sealed class PriceService
    {
        private readonly string _serviceName = "";
        private readonly ILogger<PriceService> _logger;
        private readonly RozalinaClient _rozalinaClient;
        private readonly PricesClient _pricesClient;
        private readonly FalconClient _falconClient;
        private readonly NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;
        private List<PollerStatus> _pollerUpdates = [];
        private DateTime _todaysDate;
        private bool _pricesSent = true;
        private readonly int _desiredPollingHour = 14;
        public List<ElectricityPrice> CurrentPrices { get; private set; } = [];
        public List<ElectricityPrice> TomorrowPrices { get; private set; } = [];

        public PriceService(ILogger<PriceService> logger, RozalinaClient rozalinaClient, PricesClient pricesClient, FalconClient falconClient)
        {
            _serviceName = nameof(PriceService);
            _logger = logger;
            _rozalinaClient = rozalinaClient;
            _pricesClient = pricesClient;
            _falconClient = falconClient;
        }
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
                _logger.LogInformation($"{_serviceName} running in the while loop", DateTime.Now);
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
                }
            }
        }

        private async Task UpdatePrices()
        {
            await UpdateTodayPrices();
            await UpdateTomorrowPrices();
        }
        private async Task InitializePrices()
        {
            _logger.LogInformation($"{_serviceName}:: start to initialize prices");
            List<ElectricityPrice> tempCurrent = await _falconClient.GetElectricityPrices(0);
            if (tempCurrent.Count == 0)
            {
                await UpdateTodayPrices();
            }
            else
            {
                CurrentPrices = tempCurrent;
            }

            string tomorrowDate = DateTime.Today.AddDays(1).Date.ToString("yyyy-MM-dd").Replace(".", ":");
            var tempTomorrow = await _falconClient.GetElectricityPrices(0, tomorrowDate);
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
            var pricesdto = await _pricesClient.CollectTodayPrices();
            CurrentPrices = MapDTOPrices(pricesdto!);
            await _falconClient.SendElectricityPrices(CurrentPrices);
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
            var pricesdto = await _pricesClient.CollectTomorrowPrices();
            TomorrowPrices = MapDTOPrices(pricesdto!);
            if (!_pricesSent)
            {
                await CheckForHighPriceAsync(TomorrowPrices);
                _pricesSent = true;
            }
            await _falconClient.SendElectricityPrices(TomorrowPrices);
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
                    await _rozalinaClient.SendTelegramMessage("ElectricEye", true, prices);
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
    }
}
