namespace ElectricEye.Models
{
    public class ElectricityPriceDTO
    {
        public int Rank { get; set; }
        public DateTime DateTime { get; set; }
        public double PriceWithTax { get; set; }

        public double PriceNoTax { get; set; }
    }
}
