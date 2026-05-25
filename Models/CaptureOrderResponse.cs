using PaypalServerSdk.Standard.Models;
using System.Text.Json.Serialization;

namespace Keyspeech.FunctionApp.Models
{
    internal class CaptureOrderResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("purchase_units")]
        public List<PurchaseUnit> PurchaseUnits { get; set; } = [];
    }
}