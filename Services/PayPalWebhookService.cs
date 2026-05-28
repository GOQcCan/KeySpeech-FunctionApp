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

public class PayPalWebhookService(
    HttpClient httpClient,
    ILogger<PayPalWebhookService> logger,
    PayPalConfiguration config,
    PaypalServerSdkClient paypalClient) : IPayPalWebhookService
{
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
            var cert = new X509Certificate2(certBytes);
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
            logger.LogError(ex, "Exception lors de la validation de signature PayPal");
            return false;
        }
    }

    public async Task<CaptureDetails?> GetVerifiedCaptureAsync(string captureId)
    {
        try
        {
            logger.LogInformation(
                "Vérification capture {CaptureId} via SDK officiel", captureId);

            var input = new GetCapturedPaymentInput
            {
                CaptureId = captureId
            };

            var apiResponse = await paypalClient.PaymentsController
                .GetCapturedPaymentAsync(input);

            var capture = apiResponse.Data;

            logger.LogInformation(
                "Capture {Id} — statut : {Status} — montant : {Amount} {Currency}",
                capture.Id,
                capture.Status,
                capture.Amount?.MValue,
                capture.Amount?.CurrencyCode);

            if (capture.Status != CaptureStatus.Completed)
            {
                logger.LogWarning(
                    "Capture {Id} ignorée — statut : {Status}",
                    capture.Id, capture.Status);
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
            logger.LogError(ex,
                "Erreur SDK PayPal capture {Id}: HTTP {Status} — {Message}",
                captureId, ex.ResponseCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Exception inattendue vérification capture {Id}", captureId);
            return null;
        }
    }
}