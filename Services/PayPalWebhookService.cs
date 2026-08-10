using Keyspeech.FunctionApp.Configuration;
using Keyspeech.FunctionApp.Models;
using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Models;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Keyspeech.FunctionApp.Services;

public partial class PayPalWebhookService(
    HttpClient httpClient,
    ILogger<PayPalWebhookService> logger,
    PayPalConfiguration config,
    PaypalServerSdkClient paypalClient) : IPayPalWebhookService
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Exception lors de la validation de signature PayPal")]
    private partial void LogSignatureValidationException(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Vérification capture {CaptureId} via SDK officiel")]
    private partial void LogVerifyingCapture(string captureId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Capture {Id} — statut : {Status} — montant : {Amount} {Currency}")]
    private partial void LogCaptureDetails(string? id, CaptureStatus? status, string? amount, string? currency);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Capture {Id} ignorée — statut : {Status}")]
    private partial void LogCaptureIgnored(string? id, CaptureStatus? status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erreur SDK PayPal capture {Id}: HTTP {Status} — {Message}")]
    private partial void LogApiException(Exception ex, string id, int status, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception inattendue vérification capture {Id}")]
    private partial void LogUnexpectedCaptureException(Exception ex, string id);

    public async Task<bool> ValidateSignatureAsync(
        IReadOnlyDictionary<string, string> headers,
        string rawBody)
    {
        try
        {
            // 1. Construire le message original
            string transmissionId = headers["paypal-transmission-id"];
            string transmissionTime = headers["paypal-transmission-time"];
            string certUrl = headers["paypal-cert-url"];
            string transmissionSig = headers["paypal-transmission-sig"];
            string authAlgo = headers["paypal-auth-algo"];

            // 2. CRC32 du body
            uint crc32 = BitConverter.ToUInt32(System.IO.Hashing.Crc32.Hash(Encoding.UTF8.GetBytes(rawBody)));

            // 3. Message à valider
            string message = $"{transmissionId}|{transmissionTime}|{config.WebhookId}|{crc32}";

            // 4. Télécharger le certificat PayPal
            var certBytes = await httpClient.GetByteArrayAsync(certUrl);
            var cert = X509CertificateLoader.LoadCertificate(certBytes);
            var publicKey = cert.GetRSAPublicKey()!;

            // 5. Vérifier la signature
            byte[] signatureBytes = Convert.FromBase64String(transmissionSig);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            return publicKey.VerifyData(
                messageBytes,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            LogSignatureValidationException(ex);
            return false;
        }
    }

    public async Task<CaptureDetails?> GetVerifiedCaptureAsync(string captureId)
    {
        try
        {
            LogVerifyingCapture(captureId);

            var input = new GetCapturedPaymentInput
            {
                CaptureId = captureId
            };

            var apiResponse = await paypalClient.PaymentsController
                .GetCapturedPaymentAsync(input);

            var capture = apiResponse.Data;

            LogCaptureDetails(
                capture.Id,
                capture.Status,
                capture.Amount?.MValue,
                capture.Amount?.CurrencyCode);

            if (capture.Status != CaptureStatus.Completed)
            {
                LogCaptureIgnored(capture.Id, capture.Status);
                return null;
            }

            return new CaptureDetails(
                CaptureId: capture.Id!,
                Amount: capture.Amount?.MValue ?? "0",
                Currency: capture.Amount?.CurrencyCode ?? "CAD",
                CapturedAt: capture.CreateTime ?? DateTime.UtcNow.ToString("o")
            );
        }
        catch (ApiException ex)
        {
            LogApiException(ex, captureId, ex.ResponseCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            LogUnexpectedCaptureException(ex, captureId);
            return null;
        }
    }
}