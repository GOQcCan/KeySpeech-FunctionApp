using Keyspeech.FunctionApp.Models;
using Keyspeech.FunctionApp.Services;
using Keyspeech.FunctionApp.Validation;
using Keyspeech.FunctionApp.Webhooks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Keyspeech.FunctionApp;

public partial class PayPalFunction(
    ILogger<PayPalFunction> logger,
    IPayPalWebhookService webhookService,
    IPayPalOrderService orderService,
    IPayPalCaptureService captureService,
    IPayPalEventParser eventParser,
    IWebhookEventDispatcher eventDispatcher,
    IValidator<CreateOrderRequest> createOrderValidator)
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Signature invalide — requête rejetée")]
    private partial void LogInvalidSignature();

    [LoggerMessage(Level = LogLevel.Information, Message = "Création commande pour HW: {HardwareId}")]
    private partial void LogCreatingOrder(string hardwareId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erreur lors de la création de la commande")]
    private partial void LogOrderCreationError(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Capture appelée sans token")]
    private partial void LogCaptureCalledWithoutToken();

    [LoggerMessage(Level = LogLevel.Error, Message = "Erreur lors de la capture : {OrderId}")]
    private partial void LogCaptureError(Exception ex, string orderId);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
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
        var headers = eventParser.ExtractPayPalHeaders(req.Headers);

        // 3. Valider la signature
        bool isValid = await webhookService.ValidateSignatureAsync(headers, rawBody);

        if (!isValid)
        {
            LogInvalidSignature();
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        // 4. Parser l'événement et dispatcher
        var evt = JsonSerializer.Deserialize<PayPalEvent>(rawBody, JsonOptions);

        if (evt != null)
        {
            await eventDispatcher.DispatchAsync(evt);
        }

        // PayPal exige un 200 OK rapide, sinon il retente
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function("CheckoutPayPalOrders")]
    public async Task<HttpResponseData> CheckoutPayPalOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "checkout/orders")]
        HttpRequestData req)
    {
        try
        {
            string rawBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CreateOrderRequest>(rawBody, JsonOptions);

            // Validation
            var validationResult = createOrderValidator.Validate(request!);
            if (!validationResult.IsValid)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { errors = validationResult.Errors });
                return bad;
            }

            LogCreatingOrder(request!.HardwareId);

            var result = await orderService.CreateOrderAsync(request.HardwareId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            LogOrderCreationError(ex);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Erreur lors de la création de la commande");
            return error;
        }
    }

    [Function("PayPalCapture")]
    public async Task<HttpResponseData> Capture(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "checkout/capture")] HttpRequestData req)
    {
        string? orderId = req.Query["token"];

        if (string.IsNullOrEmpty(orderId))
        {
            LogCaptureCalledWithoutToken();
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("token manquant");
            return bad;
        }

        try
        {
            var result = await captureService.CaptureOrderAsync(orderId);

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
            LogCaptureError(ex, orderId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Erreur lors de la capture");
            return error;
        }
    }
}
