using Keyspeech.FunctionApp.Configuration;
using Keyspeech.FunctionApp.Models;
using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Http.Response;
using PaypalServerSdk.Standard.Models;

namespace Keyspeech.FunctionApp.Services;

public class PayPalCheckoutService(
    ILogger<PayPalCheckoutService> logger,
    PayPalConfiguration config,
    PaypalServerSdkClient paypalClient) : IPayPalCheckoutService, IPayPalOrderService, IPayPalCaptureService
{
    public async Task<PayPalOrderResult> CreateOrderAsync(string hardwareId)
    {
        CreateOrderInput createOrderInput = new()
        {
            Body = new()
            {
                Intent = CheckoutPaymentIntent.Capture,
                PurchaseUnits =
                [
                    new() 
                    {
                        Amount = new()
                        {
                            CurrencyCode = config.Currency,
                            MValue = config.Price,
                        },
                        Description = "Licence KeySpeech",
                        CustomId = hardwareId
                    }
                ],
                ApplicationContext = new()
                {
                    BrandName = "KeySpeech",
                    UserAction = OrderApplicationContextUserAction.PayNow,
                    ReturnUrl = config.ReturnUrl
                }
            },
            Prefer = "return=minimal"
        };

        ApiResponse<Order> result = await paypalClient.OrdersController.CreateOrderAsync(createOrderInput);

        string approvalUrl = result.Data.Links.FirstOrDefault(l => l.Rel == "approve")?.Href
                                ?? throw new InvalidOperationException("Lien approve manquant");

        logger.LogInformation("Commande créée : {OrderId}", result.Data.Id);

        return new PayPalOrderResult { OrderId = result.Data.Id, ApprovalUrl = approvalUrl };
    }

    public async Task<PayPalCaptureResult> CaptureOrderAsync(string orderId)
    {
        CaptureOrderInput captureOrderInput = new()
        {
            Id = orderId,
            Prefer = "return=representation",
        };

        ApiResponse<Order> result = await paypalClient.OrdersController.CaptureOrderAsync(captureOrderInput);

        PurchaseUnit? unit = result.Data.PurchaseUnits?.FirstOrDefault();
        OrdersCapture? capture = unit?.Payments?.Captures?.FirstOrDefault();

        return new PayPalCaptureResult
        {
            OrderId = result.Data.Id ?? string.Empty,
            Status = result.Data.Status?.ToString() ?? string.Empty,
            HardwareId = capture?.CustomId ?? string.Empty,
            CaptureId = capture?.Id ?? string.Empty,
            Amount = capture?.Amount?.MValue ?? string.Empty,
            Currency = capture?.Amount?.CurrencyCode ?? string.Empty
        };
    }

    public async Task<OrderDetails> GetOrderDetailsAsync(string orderId)
    {
        Order order = await GetOrderAsync(orderId);
        string email = string.Empty;
        string firstName = string.Empty;
        string lastName = string.Empty;

        if (order.Payer != null)
        {
            email = order.Payer.EmailAddress ?? string.Empty;

            if (order.Payer.Name != null)
            {
                firstName = order.Payer.Name.GivenName ?? string.Empty;
                lastName = order.Payer.Name.Surname ?? string.Empty;
            }
        }

        return new OrderDetails
        {
            OrderId = orderId,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        };
    }

    private async Task<Order> GetOrderAsync(string orderId)
    {
        GetOrderInput getOrderInput = new()
        {
            Id = orderId,
        };

        ApiResponse<Order> result = await paypalClient.OrdersController.GetOrderAsync(getOrderInput);
        return result.Data;
    }
}