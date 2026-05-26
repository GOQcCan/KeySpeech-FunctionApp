using Keyspeech.FunctionApp.Models;
using Keyspeech.FunctionApp.Services;
using Microsoft.Extensions.Logging;

namespace Keyspeech.FunctionApp.Webhooks;

public class PaymentCaptureCompletedHandler(
    ILogger<PaymentCaptureCompletedHandler> logger,
    IPayPalEventParser eventParser,
    IOrderProcessingService orderProcessingService) : IWebhookEventHandler
{
    public string EventType => "PAYMENT.CAPTURE.COMPLETED";

    public Task<bool> CanHandleAsync(PayPalEvent evt)
    {
        return Task.FromResult(evt?.EventType == EventType);
    }

    public async Task HandleAsync(PayPalEvent evt)
    {
        // Extraire le captureId depuis le resource du webhook
        string captureId = evt.Resource.GetProperty("id").GetString()!;
        string? hardwareId = eventParser.ExtractHardwareId(evt.Resource);

        if (string.IsNullOrWhiteSpace(hardwareId))
        {
            logger.LogWarning("hardwareId manquant pour la capture {Id}", captureId);
            return;
        }

        // Extraire l'order_id depuis le resource du webhook
        string? orderId = eventParser.ExtractOrderId(evt.Resource);

        if (string.IsNullOrWhiteSpace(orderId))
        {
            logger.LogWarning("orderId manquant pour la capture {Id}", captureId);
            return;
        }

        await orderProcessingService.ProcessPaymentCompletedAsync(orderId, captureId, hardwareId);
    }
}
