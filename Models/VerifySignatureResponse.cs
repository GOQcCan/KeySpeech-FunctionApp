using System.Text.Json.Serialization;

namespace Keyspeech.FunctionApp.Models;

sealed record VerifySignatureResponse(
    [property: JsonPropertyName("verification_status")] string VerificationStatus);