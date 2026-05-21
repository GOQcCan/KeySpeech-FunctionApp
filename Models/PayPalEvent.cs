using System.Text.Json;
using System.Text.Json.Serialization;

namespace Keyspeech.PayPal.Models;

public record PayPalEvent(
    [property: JsonPropertyName("event_type")] string EventType,
    [property: JsonPropertyName("resource")] JsonElement Resource);