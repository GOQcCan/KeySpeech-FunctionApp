using Keyspeech.PayPal.Models;

namespace Keyspeech.PayPal.Services
{
    public interface IPayPalCheckoutService
    {
        Task<PayPalOrderResult> CheckoutOrdersAsync(string hardwareId);
    }
}
