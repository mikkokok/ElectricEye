
using ElectricEye.Services;
using System.Reflection;

namespace ElectricEye.Workers
{
    public sealed class ElectricEyeWorker : BackgroundService
    {
        private readonly ILogger<ElectricEyeWorker> _logger;
        private readonly ChargerService _chargerService;
        private readonly PriceService _priceService;
        private readonly string _serviceName;

        public ElectricEyeWorker(ILogger<ElectricEyeWorker> logger, [FromKeyedServices("charger")] ChargerService chargerService, [FromKeyedServices("price")]PriceService priceService)
        {
            _logger = logger;
            _chargerService = chargerService;
            _priceService = priceService;
            _serviceName = nameof(ElectricEyeWorker);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{_serviceName}:: started");
            try
            {
                Task pricePolling = RunPricePolling(stoppingToken);
                Task chargerPolling = RunChargerPolling(stoppingToken);
                await Task.WhenAll(pricePolling, chargerPolling);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"{_serviceName} is stopping");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{_serviceName} caught exception", ex);
            }
            _logger.LogInformation($"{_serviceName}:: ended");
        }

        private async Task RunPricePolling(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{_serviceName}:: starting price polling");
            while (!stoppingToken.IsCancellationRequested)
            {
                await _priceService.RunPoller(stoppingToken);
            }
            _logger.LogInformation($"{_serviceName}:: ending price polling {stoppingToken.IsCancellationRequested}");
        }

        private async Task RunChargerPolling(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{_serviceName}:: starting charger polling");
            while (!stoppingToken.IsCancellationRequested)
            {
                await _chargerService.RunPoller(stoppingToken);
            }
            _logger.LogInformation($"{_serviceName}:: ending charger polling {stoppingToken.IsCancellationRequested}");
        }
    }
}
