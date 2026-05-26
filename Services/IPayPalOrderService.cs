using Keyspeech.FunctionApp.Models;

namespace Keyspeech.FunctionApp.Services;

public interface IPayPalOrderService
{
    Task<PayPalOrderResult> CreateOrderAsync(string hardwareId);
    Task<OrderDetails> GetOrderDetailsAsync(string orderId);
}