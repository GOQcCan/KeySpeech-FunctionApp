using System.Text.Json.Serialization;

namespace Keyspeech.FunctionApp.Models
{
    internal record PayPalLink
    {
        [JsonPropertyName("href")] public string Href { get; init; } = string.Empty;
        [JsonPropertyName("rel")] public string Rel { get; init; } = string.Empty;
    }
}
