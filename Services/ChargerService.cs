using ElectricEye.Models;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using ElectricEye.Services.Clients;

namespace ElectricEye.Services
{
    public sealed class ChargerService
    {
        private readonly string _serviceName = "";
        private readonly ILogger<ChargerService> _logger;
        private readonly ChargerClient _chargerClient;
        private readonly FalconClient _falconClient;
        private List<PollerStatus> _pollerUpdates = [];
        private int _lastHour;
        private int _lastReading;
        private bool _initialPoll = true;

        public ChargerService(ILogger<ChargerService> logger, ChargerClient chargerClient, FalconClient falconClient)
        {
            _serviceName = nameof(ChargerService);
            _logger = logger;
            _chargerClient = chargerClient;
            _falconClient = falconClient;
        }
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
                                _logger.LogInformation($"{_serviceName}:: charger collecting success. ending loop");
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
                    _logger.LogInformation($"{_serviceName}:: cleaningTask status: {cleanTask.IsFaulted} ");
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

            _logger.LogInformation($"{_serviceName}:: ending carger polling");
        }
        private async Task ChargerCollector()
        {
            var reading = await _chargerClient.GetLatestConsumption();
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
                await _falconClient.SendChargingData(new CarCharge
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
                    _logger.LogInformation($"{_serviceName} cleaning updates list failed", ex.Message);
                }
            }
        }
    }
}
