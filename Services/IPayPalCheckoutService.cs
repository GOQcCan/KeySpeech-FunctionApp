using Keyspeech.FunctionApp.Models;
using System.Text.Json;

namespace Keyspeech.FunctionApp.Services
{
    public interface IPayPalCheckoutService
    {
        Task<PayPalCaptureResult> CaptureOrderAsync(string orderId);
        Task<JsonElement> GetOrderAsync(string orderId);
    }
}
