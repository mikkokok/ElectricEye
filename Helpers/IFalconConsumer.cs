using ElectricEye.Models;

namespace ElectricEye.Helpers
{
    public interface IFalconConsumer
    {
        Task<List<ElectricityPrice>> GetCurrentElectricityPrices(int ago = 0, DateTime date = default);
        Task SendElectricityPrices(List<ElectricityPrice> prices);
    }
}