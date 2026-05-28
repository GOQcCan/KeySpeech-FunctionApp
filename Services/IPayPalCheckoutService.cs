using Keyspeech.FunctionApp.Models;

namespace Keyspeech.FunctionApp.Services
{
    public interface IPayPalCheckoutService
    {
        Task<PayPalCaptureResult> CaptureOrderAsync(string orderId);
    }
}
