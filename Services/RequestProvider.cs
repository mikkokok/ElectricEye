using Microsoft.AspNetCore.Identity.Data;
using System;
using System.Net.Http;
using System.Net;
using System.Text.Json;

namespace ElectricEye.Services
{
    public sealed class RequestProvider(ILogger<RequestProvider> logger, IHttpClientFactory httpClientFactory) : IRequestProvider
    {
        private readonly string _serviceName = nameof(RequestProvider);
        private readonly ILogger<RequestProvider> _logger = logger;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly Guid _operationId = Guid.NewGuid();

        public async Task PostAsync<TRequest>(string clientName, string url, TRequest data)
        {
            _logger.LogInformation($"{_serviceName} {_operationId}:: start to send data to {url}");
            var httpClient = _httpClientFactory.CreateClient(clientName);
            try
            {
                var requestContent = SerializeToJson(data);
                _logger.LogInformation($"{_serviceName} {_operationId}:: serialization succeeded");
                using var response = await httpClient.PostAsync(url, requestContent);
                await HandleResponse(response);
                _logger.LogInformation($"{_serviceName} {_operationId}:: sending data to {url} succeeded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_serviceName} {_operationId}:: Error sending request to {url}");
                throw;
            }
        }

        public async Task<TResult?> GetAsync<TResult>(string clientName, string url)
        {
            _logger.LogInformation($"{_serviceName} {_operationId}:: start to get data to {url}");
            var httpClient = _httpClientFactory.CreateClient(clientName);
            try
            {
                using var response = await httpClient.GetAsync(url);
                await HandleResponse(response);
                var result = await ReadFromJsonASync<TResult>(response.Content);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{_serviceName} {_operationId}:: Error getting from {url}");
                throw;
            }

        }

        private static async Task HandleResponse(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }
            var content = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Request failed {response.StatusCode} with content {content}");
        }

        private static async Task<T?> ReadFromJsonASync<T>(HttpContent content)
        {
            using var contentStream = await content.ReadAsStreamAsync();
            var data = await JsonSerializer.DeserializeAsync<T>(contentStream);
            return data;
        }

        private static JsonContent SerializeToJson<T>(T data)
        {
            return JsonContent.Create(data);
        }
    }
}
