using System.Text.Json.Serialization;

namespace Keyspeech.FunctionApp.Models
{
    public record CreateOrderRequest
    {
        [JsonPropertyName("hardwareId")]
        public string HardwareId { get; init; } = string.Empty;
    }
}
