namespace ElectricEye.Helpers
{
    public static class GlobalConfig
    {
        public static string? ApiKey { get; set; }
        public static string? ChargerUrl { get; set; }
        public static PricesAPI? PricesAPIConfig { get; set; }
        public static RestlessFalcon? RestlessFalconConfig { get; set; }
        public static TelegramAPI? TelegramAPIConfig { get; set; }

        public class PricesAPI
        {
            public required string baseUrl { get; set; }
            public required string tomorrowSpotAPI { get; set; }
            public required string todaySpotAPI { get; set; }
        }

        public class RestlessFalcon
        {
            public required string baseUrl { get; set; }
            public required string electricityPriceUrl { get; set; }
            public required string chargingUrl { get; set; }
            public required string key { get; set; }
            public required string sslThumbprint { get; set; }
        }

        public class TelegramAPI
        {
            public required string url { get; set; }
            public required string key { get; set; }
        }
    }
}
