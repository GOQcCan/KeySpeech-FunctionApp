using System.Text.Json.Serialization;

namespace Keyspeech.PayPal.Models;

sealed record VerifySignatureResponse(
    [property: JsonPropertyName("verification_status")] string VerificationStatus);