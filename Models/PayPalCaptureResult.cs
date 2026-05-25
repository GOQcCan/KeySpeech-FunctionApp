namespace Keyspeech.FunctionApp.Models
{
    public class PayPalCaptureResult
    {
        public string OrderId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
        public string CaptureId { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
    }
}