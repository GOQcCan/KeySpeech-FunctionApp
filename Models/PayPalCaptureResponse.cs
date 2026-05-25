using PaypalServerSdk.Standard.Models;
using System.Text.Json.Serialization;

namespace Keyspeech.FunctionApp.Models
{
    public class PayPalCaptureResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;          // OrderId

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;      // COMPLETED

        [JsonPropertyName("purchase_units")]
        public List<PurchaseUnit> PurchaseUnits { get; set; } = [];
    }
}