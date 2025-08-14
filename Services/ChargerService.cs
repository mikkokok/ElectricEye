using ElectricEye.Models;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using ElectricEye.Helpers;
using ElectricEye.Constants;

namespace ElectricEye.Services
{
    public sealed class ChargerService(ILogger<ChargerService> logger, IRequestProvider requestProvider)
    {
        private readonly string _serviceName = nameof(ChargerService);
        private readonly ILogger<ChargerService> _logger = logger;
        private readonly IRequestProvider _requestProvider = requestProvider;
        private List<PollerStatus> _pollerUpdates = [];
        private int _lastHour;
        private int _lastReading;
        private bool _initialPoll = true;

        public List<PollerStatus> GetStatus()
        {
            return _pollerUpdates;
        }

        public async Task RunPoller(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{_serviceName}:: starting charger polling");
            Task cleanTask = CleanUpdatesList(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (CalculateExactHour())
                    {
                        for (int i = 1; i < 3; i++)
                        {
                            try
                            {
                                await ChargerCollector();
                                _logger.LogInformation($"{_serviceName}:: charger collecting success. ending loop. Cleanin task status {cleanTask.Status}");
                                break;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"{_serviceName}:: {ex.Message}");
                                _pollerUpdates.Add(new PollerStatus
                                {
                                    Time = DateTime.Now,
                                    Poller = _serviceName,
                                    Status = false,
                                    StatusReason = $"Charger polling failed, errormessage {ex.Message}, continuing with {i}/3 retries"
                                });
                                _logger.LogInformation($"{_serviceName}:: continuing with {i}/3 retries");
                                if (i >= 3)
                                {
                                    _logger.LogInformation($"{_serviceName}:: {i}/3 retries, passed limit");
                                    throw new Exception("Retries done, exiting");
                                }
                                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                            }
                        }
                    }
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _pollerUpdates.Add(new PollerStatus
                    {
                        Time = DateTime.Now,
                        Poller = _serviceName,
                        Status = false,
                        StatusReason = ex.Message
                    });
                }
            }
            _logger.LogInformation($"{_serviceName}:: ending carger polling, token {stoppingToken.IsCancellationRequested}");
        }
        private async Task ChargerCollector()
        {
            var reading = await GetLatestConsumption();
            _logger.LogInformation($"{_serviceName}:: got {reading} for latest consumption");

            if (!_initialPoll)
            {
                if (reading.eto < _lastReading || reading.eto == 0)
                {
                    throw new Exception($"{_serviceName}:: Could not get reasonable consumption value, value being {reading.eto}");
                }
            }

            if (_lastReading < reading.eto && !_initialPoll)
            {
                DateTime now = DateTime.Now;
                DateTime rounded = new(now.Year, now.Month, now.Day, now.Hour, 0, 0);
                await SendChargingData(new CarCharge
                {
                    date = rounded.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss").Replace(".", ":"),
                    charged = CalculateDifferenceAndConvert(reading.eto).ToString(),
                    hour = DateTime.Now.AddHours(-1).Hour
                });
            }

            _pollerUpdates.Add(new PollerStatus
            {
                Time = DateTime.Now,
                Poller = _serviceName,
                Status = true,
                StatusReason = $"Successfully got data {reading.eto} from charger initial poll value being {_initialPoll}"
            });

            _lastReading = reading.eto;
            _initialPoll = false;
            _logger.LogInformation($"{_serviceName}:: ended run of ChargerCollector {DateTime.Now} lastReading: {_lastReading} initialPoll: {_initialPoll}");
        }

        private bool CalculateExactHour()
        {
            if (_lastHour < DateTime.Now.Hour || (_lastHour == 23 && DateTime.Now.Hour == 0))
            {
                _lastHour = DateTime.Now.Hour;
                return true;
            }
            return false;
        }
        private float CalculateDifferenceAndConvert(int total)
        {
            float consumed = total - _lastReading;
            return consumed / 1000;
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
                    }
                    await Task.Delay(TimeSpan.FromMinutes(45), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"{_serviceName} cleaning updates list failed, token {stoppingToken.IsCancellationRequested}", ex.Message);
                }
            }
        }
        private async Task<ChargerDTO> GetLatestConsumption()
        {
            var result = await _requestProvider.GetAsync<ChargerDTO>(HttpClientConst.ChargerClientName, GlobalConfig.ChargerUrl!);
            return result ?? throw new Exception($"Getting latest readings from {GlobalConfig.ChargerUrl!} failed");
        }

        private async Task SendChargingData(CarCharge charge)
        {
            var url = GlobalConfig.RestlessFalconConfig!.baseUrl + GlobalConfig.RestlessFalconConfig.chargingUrl;
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["authKey"] = GlobalConfig.RestlessFalconConfig.key;
            url = string.Join("?", url, query.ToString());
            await _requestProvider.PostAsync(HttpClientConst.FalconClientName, url, charge);
        }
    }
}
