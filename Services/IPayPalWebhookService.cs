using Keyspeech.FunctionApp.Models;

namespace Keyspeech.FunctionApp.Services;

public interface IPayPalWebhookService
{
    Task<CaptureDetails?> GetVerifiedCaptureAsync(string captureId);
    Task<bool> ValidateSignatureAsync(
        IReadOnlyDictionary<string, string> headers,
        string rawBody);
}