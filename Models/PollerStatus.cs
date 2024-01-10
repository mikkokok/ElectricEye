namespace ElectricEye.Models
{
    public class PollerStatus
    {
        public DateTime Time { get; set; }
        public string? Poller { get; set; }
        public bool? Status { get; set; }
        public string? StatusReason { get; set; }
    }
}
