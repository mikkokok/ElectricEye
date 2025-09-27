
using ElectricEye.Services;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace ElectricEye.Workers
{
    public sealed class ElectricEyeWorker : BackgroundService
    {
        private readonly ILogger<ElectricEyeWorker> _logger;
        private readonly ChargerService _chargerService;
        private readonly PriceService _priceService;
        private readonly string _serviceName;

        public ElectricEyeWorker(ILogger<ElectricEyeWorker> logger, [FromServices] ChargerService chargerService, [FromServices] PriceService priceService)
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
                using var priceCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                using var chargerCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                _priceService.PriceTask = RunPricePolling(priceCts.Token);
                _chargerService.ChargerTask = RunChargerPolling(chargerCts.Token);
                var completedTask = await Task.WhenAny(_priceService.PriceTask, _chargerService.ChargerTask);

                if (completedTask == _priceService.PriceTask)
                {
                    _logger.LogWarning($"{_serviceName}:: Price polling task completed unexpectedly, cancelling charger polling...");
                    chargerCts.Cancel();
                }
                else if (completedTask == _chargerService.ChargerTask)
                {
                    _logger.LogWarning($"{_serviceName}:: Charger polling task completed unexpectedly, cancelling price polling...");
                    priceCts.Cancel();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"{_serviceName} is stopping");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{_serviceName} caught unexpected exception", ex);
            }
            _logger.LogInformation($"{_serviceName}:: ended");
        }

        private async Task RunPricePolling(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{_serviceName}:: starting price polling");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _priceService.RunPoller(stoppingToken);
                    _logger.LogWarning($"{_serviceName}:: RunPoller completed unexpectedly, restarting...");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation($"{_serviceName}:: Price polling cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{_serviceName}:: Price polling failed, restarting in 30 seconds...");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                _logger.LogError($"{_serviceName}:: Price polling failed really unexpectedly, restarting in 30 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            _logger.LogInformation($"{_serviceName}:: ending price polling {stoppingToken.IsCancellationRequested}");

        }

        private async Task RunChargerPolling(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{_serviceName}:: starting charger polling");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _chargerService.RunPoller(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation($"{_serviceName}:: Charger polling cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{_serviceName}:: Charger polling failed, restarting in 30 seconds...");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                _logger.LogError($"{_serviceName}:: Charger polling failed really unexpectedly, restarting in 30 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            }
            _logger.LogInformation($"{_serviceName}:: ending charger polling {stoppingToken.IsCancellationRequested}");
        }
    }
}
