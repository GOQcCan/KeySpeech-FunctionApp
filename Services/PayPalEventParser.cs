using Microsoft.Azure.Functions.Worker.Http;
using System.Text.Json;

namespace Keyspeech.FunctionApp.Services;

public class PayPalEventParser : IPayPalEventParser
{
    private static readonly string[] RequiredHeaders =
    [
        "paypal-auth-algo",
        "paypal-cert-url",
        "paypal-transmission-id",
        "paypal-transmission-sig",
        "paypal-transmission-time"
    ];

    public Dictionary<string, string> ExtractPayPalHeaders(HttpHeadersCollection headers)
    {
        return RequiredHeaders.ToDictionary(
            key => key,
            key => headers.TryGetValues(key, out var values)
                ? values.First()
                : string.Empty,
            StringComparer.OrdinalIgnoreCase);
    }

    public string? ExtractHardwareId(JsonElement resource)
    {
        // PAYMENT.SALE.COMPLETED → custom_id directement sur resource
        if (resource.TryGetProperty("custom_id", out var customIdProp))
            return customIdProp.GetString();

        // PAYMENT.CAPTURE.COMPLETED → dans purchase_units[0]
        if (resource.TryGetProperty("purchase_units", out var units) &&
            units.ValueKind == JsonValueKind.Array &&
            units.GetArrayLength() > 0)
        {
            var firstUnit = units[0];
            if (firstUnit.TryGetProperty("custom_id", out var unitCustomId))
                return unitCustomId.GetString();
        }

        return null;
    }

    public string? ExtractOrderId(JsonElement resource)
    {
        if (resource.TryGetProperty("supplementary_data", out var sup) &&
            sup.TryGetProperty("related_ids", out var rel) &&
            rel.TryGetProperty("order_id", out var orderIdProp))
        {
            return orderIdProp.GetString();
        }

        return null;
    }
}
