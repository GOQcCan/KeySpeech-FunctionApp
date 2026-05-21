using System.Text.Json.Serialization;

namespace Keyspeech.FunctionApp.Models;

sealed record WebhookVerifyResponse(
    [property: JsonPropertyName("verification_status")] string VerificationStatus);