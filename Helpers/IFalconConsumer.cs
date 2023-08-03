using ElectricEye.Models;

namespace ElectricEye.Helpers
{
    public interface IFalconConsumer
    {
        Task<List<ElectricityPrice>> GetElectricityPrices(int ago = 0, string date = "");
        Task SendElectricityPrices(List<ElectricityPrice> prices);
    }
}