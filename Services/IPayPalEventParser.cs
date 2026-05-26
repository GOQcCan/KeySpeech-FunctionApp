using Microsoft.Azure.Functions.Worker.Http;
using System.Text.Json;

namespace Keyspeech.FunctionApp.Services
{
    public interface IPayPalEventParser
    {
        Dictionary<string, string> ExtractPayPalHeaders(HttpHeadersCollection headers);
        string? ExtractHardwareId(JsonElement resource);
        string? ExtractOrderId(JsonElement resource);
    }
}
