using Keyspeech.FunctionApp.Models;

namespace Keyspeech.FunctionApp.Services
{
    public interface IPayPalCheckoutService
    {
        Task<PayPalOrderResult> CheckoutOrdersAsync(string hardwareId);
    }
}
