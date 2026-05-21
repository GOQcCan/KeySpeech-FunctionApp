using System.Text.Json.Serialization;

namespace Keyspeech.FunctionApp.Models
{
    internal record PayPalOrderResponse
    {
        [JsonPropertyName("id")] 
        public string Id { get; init; } = string.Empty;
        [JsonPropertyName("links")] 
        public PayPalLink[] Links { get; init; } = [];
    }
}
