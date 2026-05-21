using System.Text.Json.Serialization;

namespace Keyspeech.PayPal.Models
{
    internal record OAuthResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }
}
