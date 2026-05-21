using Keyspeech.PayPal.Models;

namespace Keyspeech.PayPal.Services;

public interface IPayPalWebhookService
{
    Task<CaptureDetails?> GetVerifiedCaptureAsync(string captureId);
    Task<bool> ValidateSignatureAsync(
        IReadOnlyDictionary<string, string> headers,
        string rawBody);
}