using ElectricEye.Models;

namespace ElectricEye.Helpers
{
    public interface IApiPoller
    {
        List<ElectricityPrice> GetCurrentPrices();

        List<ElectricityPrice> GetTomorrowPrices();
        List<PollerStatus> GetStatus();
        bool IsRunning { get; set; }
    }
}