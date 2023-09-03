﻿using ElectricEye.Models;
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
        private bool _polling = false;
        private string _pollingUrl;
        private int _lastHour;
        private int _lastReading;
        private bool _retry;
        private readonly FalconConsumer _falconConsumer;


        public ChargerPoller(IConfiguration config, FalconConsumer falconConsumer)
        {
            _falconConsumer = falconConsumer;
            _config = config;
            _pollingUrl = _config["ChargerUrl"];
            _ = StartCollectors();
        }

        private async Task StartCollectors()
        {
            _polling = true;
            while (_polling)
            {
                if (CalculateExactHour())
                {
                    await ChargerCollector();
                }
                Thread.Sleep(TimeSpan.FromSeconds(30));
            }
            _polling = false;
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
                if (reading == null || reading.eto > _lastReading || reading.eto == 0) {
                    throw new Exception($"Could not get reasonable values from {_pollingUrl}, value being {reading?.eto}");
                }
                if (_lastReading < reading.eto)
                {
                    InvokeFalcon(new CarCharge
                    {
                        Date = DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss").Replace(".", ":"),
                        Charged = ConvertWhTokWh(reading.eto).ToString(),
                        Hour = DateTime.Now.AddHours(-1).Hour
                    });
                }
                                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Charger polling {_pollingUrl} failed, errormessage {ex.Message}, continuing");
            }
        }
        private async Task<JsonObject> QueryCharger(string url)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonNode.Parse(responseContent).AsObject();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Polling Shelly device failed from url {url}, errormessage {ex.Message}, retrying");
                if (_retry)
                    await QueryCharger(url);
                Console.WriteLine($"Polling Shelly device failed from url {url}, errormessage {ex.Message}, failing");
            }
            throw new Exception($"Polling Shelly device failed from url {url}, failing");

        }
        private void InvokeFalcon(CarCharge chargeData)
        {
            _ = Task.Run(async () => await _falconConsumer.SendChargingData(chargeData));
        }

        private static float ConvertWhTokWh(int total)
        {
            return total / 1000;
        }
    }
}
