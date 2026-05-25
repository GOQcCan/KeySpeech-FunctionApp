using Keyspeech.FunctionApp.Models;
using Keyspeech.FunctionApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard.Models;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Keyspeech.FunctionApp;

public class PayPalFunction(
    ILogger<PayPalFunction> logger,
    IPayPalWebhookService webhookService,
    IPayPalCheckoutService checkoutService,
    ILicenseService licenseService,
    IEmailService emailService)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,                          // pour la désérialisation
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,      // pour la sérialisation (.NET 8+)
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Function("HandlePayPalWebhook")]
    public async Task<HttpResponseData> HandlePayPalWebhook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhook/paypal")]
        HttpRequestData req)
    {
        // 1. Lire le body brut AVANT tout autre traitement
        string rawBody = await new StreamReader(req.Body).ReadToEndAsync();

        // 2. Extraire les headers PayPal
        var headers = ExtractPayPalHeaders(req.Headers);

        // 3. Valider la signature
        bool isValid = await webhookService.ValidateSignatureAsync(headers, rawBody);

        if (!isValid)
        {
            logger.LogWarning("Signature invalide — requête rejetée");
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        // 4. Parser l'événement
        var evt = JsonSerializer.Deserialize<PayPalEvent>(rawBody, JsonOptions);

        if (evt?.EventType == "PAYMENT.CAPTURE.COMPLETED")
        {
            // Extraire le captureId depuis le resource du webhook
            string captureId = evt.Resource.GetProperty("id").GetString()!;
            string? hardwareId = ExtractHardwareId(evt.Resource);

            if (string.IsNullOrWhiteSpace(hardwareId))
            {
                logger.LogWarning("hardwareId manquant pour la capture {Id}", captureId);
                return req.CreateResponse(HttpStatusCode.OK);
            }

            // Extraire l'order_id depuis le resource du webhook
            string? orderId = null;
            if (evt.Resource.TryGetProperty("supplementary_data", out var sup) &&
                sup.TryGetProperty("related_ids", out var rel) &&
                rel.TryGetProperty("order_id", out var orderIdProp))
            {
                orderId = orderIdProp.GetString();
            }

            if (string.IsNullOrWhiteSpace(orderId))
            {
                logger.LogWarning("orderId manquant pour la capture {Id}", captureId);
                return req.CreateResponse(HttpStatusCode.OK);
            }

            await HandlePaymentCompleted(orderId, captureId, hardwareId);
        }
        else
        {
            logger.LogInformation("Événement ignoré : {Type}", evt?.EventType);
        }

        // PayPal exige un 200 OK rapide, sinon il retente
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function("CheckoutPayPalOrders")]
    public async Task<HttpResponseData> CreatePayPalOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "checkout/orders")]
        HttpRequestData req)
    {
        string rawBody = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<CreateOrderRequest>(rawBody, JsonOptions);

        if (string.IsNullOrWhiteSpace(request?.HardwareId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("hardwareId requis");
            return bad;
        }

        logger.LogInformation("Création commande pour HW: {HardwareId}", request.HardwareId);

        var result = await checkoutService.CheckoutOrdersAsync(request.HardwareId);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("PayPalCapture")]
    public async Task<HttpResponseData> Capture(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "checkout/capture")] HttpRequestData req)
    {
        string? orderId = req.Query["token"];

        if (string.IsNullOrEmpty(orderId))
        {
            logger.LogWarning("Capture appelée sans token");
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("token manquant");
            return bad;
        }

        try
        {
            var result = await checkoutService.CaptureOrderAsync(orderId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");

            await response.WriteStringAsync(result.Status == "Completed" ? """
                <html><body style="font-family:sans-serif;text-align:center;padding:50px">
                    <h1>✅ Paiement réussi !</h1>
                    <p>Votre licence KeySpeech est en cours d'activation.</p>
                    <p>Vous pouvez fermer cette fenêtre.</p>
                </body></html>
                """ : """
                <html><body style="font-family:sans-serif;text-align:center;padding:50px">
                    <h1>❌ Paiement échoué</h1>
                    <p>Veuillez réessayer.</p>
                </body></html>
                """);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la capture : {OrderId}", orderId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Erreur lors de la capture");
            return error;
        }
    }

    private async Task HandlePaymentCompleted(
        string orderId,
        string captureId,
        string hardwareId)
    {
        // 1. Double vérification via le SDK officiel
        //    On confirme que la capture est réellement COMPLETED côté PayPal
        var capture = await webhookService.GetVerifiedCaptureAsync(captureId);

        if (capture is null)
        {
            logger.LogWarning(
                "Capture {Id} non confirmée par PayPal — licence non générée",
                captureId);
            return;
        }

        // 2. Extraire les infos du client depuis le payload webhook
        var order = await checkoutService.GetOrderAsync(orderId);
        string email = string.Empty;
        string firstName = string.Empty;
        string lastName = string.Empty;

        if (order.TryGetProperty("payer", out var payer))
        {
            email = payer.TryGetProperty("email_address", out var e)
                        ? e.GetString()! : string.Empty;

            if (string.IsNullOrEmpty(email))
            {
                logger.LogWarning("email manquant pour la capture {Id}", captureId);
                return;
            }

            if (payer.TryGetProperty("name", out var name))
            {
                firstName = name.TryGetProperty("given_name", out var fn)
                            ? fn.GetString()! : string.Empty;
                lastName = name.TryGetProperty("surname", out var ln)
                            ? ln.GetString()! : string.Empty;
            }
        }

        var fullName = $"{firstName} {lastName}";

        logger.LogInformation(
            "Paiement confirmé — {Id} | {Amount} {Currency} | {Email}",
            capture.CaptureId, capture.Amount, capture.Currency, email);

        // 3. Générer la licence .NET Reactor
        byte[] licenseKey = licenseService.GenerateLicense(hardwareId);

        // 4. Envoyer l'email au client
        await emailService.SendLicenseAsync(email, fullName, licenseKey);

        logger.LogInformation("Licence envoyée à {Email}", email);
    }

    private static Dictionary<string, string> ExtractPayPalHeaders(
        HttpHeadersCollection headers)
    {
        var keys = new[]
        {
            "paypal-auth-algo",
            "paypal-cert-url",
            "paypal-transmission-id",
            "paypal-transmission-sig",
            "paypal-transmission-time"
        };

        return keys.ToDictionary(
            key => key,
            key => headers.TryGetValues(key, out var values)
                ? values.First()
                : string.Empty,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string? ExtractHardwareId(JsonElement resource)
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
}