using Microsoft.Extensions.Logging;

namespace Keyspeech.FunctionApp.Services;

public partial class OrderProcessingService(
    ILogger<OrderProcessingService> logger,
    IPayPalWebhookService webhookService,
    IPayPalOrderService orderService,
    ILicenseService licenseService,
    IEmailService emailService) : IOrderProcessingService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Capture {CaptureId} non confirmée par PayPal — licence non générée")]
    private partial void LogCaptureNotConfirmed(string captureId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email manquant pour la capture {CaptureId}")]
    private partial void LogEmailMissing(string captureId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Paiement confirmé — {CaptureId} | {Amount} {Currency} | {Email}")]
    private partial void LogPaymentConfirmed(string captureId, string amount, string currency, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Licence envoyée à {Email}")]
    private partial void LogLicenseSent(string email);

    public async Task ProcessPaymentCompletedAsync(string orderId, string captureId, string hardwareId)
    {
        // 1. Double vérification via le SDK officiel
        //    On confirme que la capture est réellement COMPLETED côté PayPal
        var capture = await webhookService.GetVerifiedCaptureAsync(captureId);

        if (capture is null)
        {
            LogCaptureNotConfirmed(captureId);
            return;
        }

        // 2. Extraire les infos du client depuis l'order
        var orderDetails = await orderService.GetOrderDetailsAsync(orderId);

        if (string.IsNullOrEmpty(orderDetails.Email))
        {
            LogEmailMissing(captureId);
            return;
        }

        LogPaymentConfirmed(capture.CaptureId, capture.Amount, capture.Currency, orderDetails.Email);

        // 3. Générer la licence .NET Reactor
        byte[] licenseKey = licenseService.GenerateLicense(hardwareId);

        // 4. Envoyer l'email au client
        await emailService.SendLicenseAsync(orderDetails.Email, orderDetails.FullName, licenseKey);

        LogLicenseSent(orderDetails.Email);
    }
}
