using ElectricEye.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;

namespace ElectricEye.Helpers.Impl
{
    public class ChargerPoller : BackgroundService, IChargerPoller
    {
        private readonly IConfiguration _config;
        public bool IsRunning { get; set; }
        private const string _pollerName = "ChargerPoller";
        private readonly HttpClient _httpClient;
        private string _pollingUrl;
        private int _lastHour;
        private int _lastReading;
        private readonly FalconConsumer _falconConsumer;
        private bool _initialPoll = true;
        private static List<PollerStatus> _pollerUpdates = new();

        public ChargerPoller(IConfiguration config)
        {
            _config = config;
            _falconConsumer = new FalconConsumer(_config);
            _pollingUrl = _config["ChargerUrl"];
            _httpClient = new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 30)
            };
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var CleaningTask = CleanUpdatesList();
            var CollectorTask = StartCollectors();
            try
            {
                await Task.WhenAll(CleaningTask, CollectorTask);
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

        public List<PollerStatus> GetStatus()
        {
            return _pollerUpdates;
        }

        private async Task StartCollectors()
        {
            IsRunning = true;
            while (IsRunning)
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
                                break;
                            }
                            catch (Exception ex)
                            {
                                _pollerUpdates.Add(new PollerStatus
                                {
                                    Time = DateTime.Now,
                                    Poller = _pollerName,
                                    Status = false,
                                    StatusReason = $"Charger polling {_pollingUrl} failed, errormessage {ex.Message}, continuing with {i}/3 retries"
                                });
                                if (i >= 3)
                                {
                                    throw new Exception("Retries done, exiting");
                                }
                                await Task.Delay(TimeSpan.FromSeconds(30));
                            }
                        }
                    }
                    await Task.Delay(TimeSpan.FromSeconds(30));
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
        private bool CalculateExactHour()
        {
            if (_lastHour < DateTime.Now.Hour || (_lastHour == 23 && DateTime.Now.Hour == 0))
            {
                _lastHour = DateTime.Now.Hour;
                return true;
            }
            return false;
        }

        private async Task ChargerCollector()
        {
            var uriBuilder = new UriBuilder(_pollingUrl)
            {
                Scheme = Uri.UriSchemeHttp,
                Port = 80
            };
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);

            uriBuilder.Query = query.ToString();
            using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var reading = JsonSerializer.Deserialize<ChargerDTO>(responseContent);

            if (reading == null)
            {
                throw new Exception($"Could not get reasonable values from {_pollingUrl}, reading was null");
            }

            if (!_initialPoll)
            {
                if (reading.eto < _lastReading || reading.eto == 0)
                {
                    throw new Exception($"Could not get reasonable values from {_pollingUrl}, value being {reading?.eto}");
                }
            }

            if (_lastReading < reading.eto && !_initialPoll)
            {
                DateTime now = DateTime.Now;
                DateTime rounded = new(now.Year, now.Month, now.Day, now.Hour, 0, 0);
                await _falconConsumer.SendChargingData(new CarCharge
                {
                    date = rounded.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss").Replace(".", ":"),
                    charged = CalculateDifferenceAndCovert(reading.eto).ToString(),
                    hour = DateTime.Now.AddHours(-1).Hour
                });
            }

            _pollerUpdates.Add(new PollerStatus
            {
                Time = DateTime.Now,
                Poller = _pollerName,
                Status = true,
                StatusReason = $"Successfully got data {reading.eto} from charger initial poll value being {_initialPoll}"
            });

            _lastReading = reading.eto;
            _initialPoll = false;
        }
        private float CalculateDifferenceAndCovert(int total)
        {
            float consumed = total - _lastReading;
            return consumed / 1000;
        }

        private static async Task CleanUpdatesList()
        {
            while (true)
            {
                try
                {
                    if (DateTime.Now.Day == 28 && DateTime.Now.Hour == 23)
                    {
                        _pollerUpdates.Clear();
                    }
                    await Task.Delay(TimeSpan.FromMinutes(45));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}