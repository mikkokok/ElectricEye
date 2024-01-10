using ElectricEye.Models;

namespace ElectricEye.Helpers
{
    public interface IApiPoller
    {
        List<ElectricityPrice> CurrentPrices { get; }
        List<ElectricityPrice> TomorrowPrices { get; }
        List<PollerStatus> PollerUpdates { get; }
        bool IsRunning { get; set; }
    }
}