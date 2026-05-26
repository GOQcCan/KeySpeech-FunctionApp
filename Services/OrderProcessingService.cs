using Microsoft.Extensions.Logging;

namespace Keyspeech.FunctionApp.Services;

public class OrderProcessingService(
    ILogger<OrderProcessingService> logger,
    IPayPalWebhookService webhookService,
    IPayPalOrderService orderService,
    ILicenseService licenseService,
    IEmailService emailService) : IOrderProcessingService
{
    public async Task ProcessPaymentCompletedAsync(string orderId, string captureId, string hardwareId)
    {
        // 1. Double vérification via le SDK officiel
        //    On confirme que la capture est réellement COMPLETED côté PayPal
        var capture = await webhookService.GetVerifiedCaptureAsync(captureId);

        if (capture is null)
        {
            logger.LogWarning(
                "Capture {CaptureId} non confirmée par PayPal — licence non générée",
                captureId);
            return;
        }

        // 2. Extraire les infos du client depuis l'order
        var orderDetails = await orderService.GetOrderDetailsAsync(orderId);

        if (string.IsNullOrEmpty(orderDetails.Email))
        {
            logger.LogWarning("Email manquant pour la capture {CaptureId}", captureId);
            return;
        }

        logger.LogInformation(
            "Paiement confirmé — {CaptureId} | {Amount} {Currency} | {Email}",
            capture.CaptureId, capture.Amount, capture.Currency, orderDetails.Email);

        // 3. Générer la licence .NET Reactor
        byte[] licenseKey = licenseService.GenerateLicense(hardwareId);

        // 4. Envoyer l'email au client
        await emailService.SendLicenseAsync(orderDetails.Email, orderDetails.FullName, licenseKey);

        logger.LogInformation("Licence envoyée à {Email}", orderDetails.Email);
    }
}
