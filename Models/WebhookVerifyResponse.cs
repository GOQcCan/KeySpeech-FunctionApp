using System.Text.Json.Serialization;

namespace Keyspeech.PayPal.Models;

sealed record WebhookVerifyResponse(
    [property: JsonPropertyName("verification_status")] string VerificationStatus);