using Keyspeech.FunctionApp.Models;

namespace Keyspeech.FunctionApp.Services;

public interface IPayPalCaptureService
{
    Task<PayPalCaptureResult> CaptureOrderAsync(string orderId);
}
