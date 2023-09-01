using ElectricEye.Models;

namespace ElectricEye.Helpers
{
    public interface ITelegramBotConsumer
    {
        Task SendTelegramMessage(string from, bool admin, List<ElectricityPrice> electricityPrices);
    }
}