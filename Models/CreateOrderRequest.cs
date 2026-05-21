using System.Text.Json.Serialization;

namespace Keyspeech.PayPal.Models
{
    internal record CreateOrderRequest
    {
        [JsonPropertyName("hardwareId")]
        public string HardwareId { get; init; } = string.Empty;
    }
}
