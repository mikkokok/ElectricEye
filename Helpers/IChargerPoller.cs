using ElectricEye.Models;

namespace ElectricEye.Helpers
{
    public interface IChargerPoller
    {
        bool IsRunning { get; set; }
        PollerStatus GetStatus();
    }
}