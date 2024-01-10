﻿using ElectricEye.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;

namespace ElectricEye.Helpers.Impl
{
    public class ChargerPoller : IChargerPoller
    {
        private readonly IConfiguration _config;
        public bool IsRunning { get; set; }
        private const string _pollerName = "ApiPoller";
        private readonly HttpClient _httpClient;
        private string _pollingUrl;
        private int _lastHour;
        private int _lastReading;
        private readonly FalconConsumer _falconConsumer;
        private bool _initialPoll = true;
        public List<PollerStatus> PollerUpdates { get; private set; }

        public ChargerPoller(IConfiguration config, FalconConsumer falconConsumer)
        {
            _falconConsumer = falconConsumer;
            _config = config;
            _pollingUrl = _config["ChargerUrl"];
            _httpClient = new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 30)
            };
            PollerUpdates = new List<PollerStatus>();
            Task.Run(StartCollectors);
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
                                PollerUpdates.Add(new PollerStatus
                                {
                                    Time = DateTime.Now,
                                    Poller = _pollerName,
                                    Status = false,
                                    StatusReason = $"Charger polling {_pollingUrl} failed, errormessage {ex.Message}, continuing with {i}/3 retries"
                                });
                                if (i == 3)
                                {
                                    throw;
                                }
                                await Task.Delay(TimeSpan.FromSeconds(30));
                            }
                        }
                    }
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
                catch (Exception ex)
                {
                    PollerUpdates.Add(new PollerStatus
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
                DateTime rounded = new(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
                await _falconConsumer.SendChargingData(new CarCharge
                {
                    date = rounded.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss").Replace(".", ":"),
                    charged = CalculateDifferenceAndCovert(reading.eto).ToString(),
                    hour = DateTime.Now.AddHours(-1).Hour
                });
            }

            PollerUpdates.Add(new PollerStatus
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
    }
}