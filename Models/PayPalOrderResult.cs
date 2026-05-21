namespace Keyspeech.FunctionApp.Models
{
    public record PayPalOrderResult
    {
        public string OrderId { get; init; } = string.Empty;
        public string ApprovalUrl { get; init; } = string.Empty;
    }
}
