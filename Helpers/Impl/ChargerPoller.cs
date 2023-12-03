using ElectricEye.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace ElectricEye.Helpers.Impl
{
    public class ChargerPoller : IChargerPoller
    {
        private readonly IConfiguration _config;
        public bool IsRunning { get; set; }
        private readonly HttpClient _httpClient;
        private string _pollingUrl;
        private int _lastHour;
        private int _lastReading;
        private readonly FalconConsumer _falconConsumer;
        private bool _initialPoll = true;
        private string _latestException;


        public ChargerPoller(IConfiguration config, FalconConsumer falconConsumer)
        {
            _falconConsumer = falconConsumer;
            _config = config;
            _pollingUrl = _config["ChargerUrl"];
            _latestException = "No exceptions :)";
            _httpClient = new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 30)
            };
            _ = StartCollectors();
        }
        public PollerStatus GetStatus()
        {
            return new PollerStatus
            {
                Poller = "ChargerPoller",
                Status = IsRunning,
                StatusReason = _latestException
            };
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
                        await ChargerCollector();
                    }
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
                catch (Exception ex)
                {
                    _latestException = ex.Message;
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
            try
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                var reading = JsonSerializer.Deserialize<ChargerDTO>(responseContent);
                if (!_initialPoll)
                {
                    if (reading == null || reading.eto < _lastReading || reading.eto == 0)
                    {
                        throw new Exception($"Could not get reasonable values from {_pollingUrl}, value being {reading?.eto}");
                    }

                }

                if (_lastReading < reading.eto && !_initialPoll)
                {
                    InvokeFalcon(new CarCharge
                    {
                        date = DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss").Replace(".", ":"),
                        charged = CalculateDifferenceAndCovert(reading.eto).ToString(),
                        hour = DateTime.Now.AddHours(-1).Hour
                    });
                }
                _lastReading = reading.eto;
                _initialPoll = false;
                                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Charger polling {_pollingUrl} failed, errormessage {ex.Message}, continuing");
            }
        }
        private void InvokeFalcon(CarCharge chargeData)
        {
            _ = Task.Run(async () => await _falconConsumer.SendChargingData(chargeData));
        }

        private float CalculateDifferenceAndCovert(int total)
        {
            float consumed = total - _lastReading;
            return consumed / 1000;
        }
    }
}
