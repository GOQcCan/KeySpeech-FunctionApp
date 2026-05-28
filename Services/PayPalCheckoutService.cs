using Keyspeech.FunctionApp.Configuration;
using Keyspeech.FunctionApp.Models;
using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Http.Response;
using PaypalServerSdk.Standard.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Keyspeech.FunctionApp.Services;

public class PayPalCheckoutService(
    IHttpClientFactory httpClientFactory,
    ILogger<PayPalCheckoutService> logger,
    PayPalConfiguration config,
    PaypalServerSdkClient paypalClient) : IPayPalCheckoutService, IPayPalOrderService, IPayPalCaptureService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<PayPalOrderResult> CreateOrderAsync(string hardwareId)
    {
        CreateOrderInput createOrderInput = new()
        {
            Body = new OrderRequest
            {
                Intent = CheckoutPaymentIntent.Capture,
                PurchaseUnits =
                [
                    new() 
                    {
                        Amount = new AmountWithBreakdown
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

    public async Task<JsonElement> GetOrderAsync(string orderId)
    {
        HttpClient http = httpClientFactory.CreateClient("PayPal");
        string accessToken = await GetAccessTokenAsync(http, config.BaseUrl, config.ClientId, config.ClientSecret);

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{config.BaseUrl}/v2/checkout/orders/{orderId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json);
    }

    public async Task<OrderDetails> GetOrderDetailsAsync(string orderId)
    {
        var order = await GetOrderAsync(orderId);

        string email = string.Empty;
        string firstName = string.Empty;
        string lastName = string.Empty;

        if (order.TryGetProperty("payer", out var payer))
        {
            email = payer.TryGetProperty("email_address", out var e)
                        ? e.GetString()! : string.Empty;

            if (payer.TryGetProperty("name", out var name))
            {
                firstName = name.TryGetProperty("given_name", out var fn)
                            ? fn.GetString()! : string.Empty;
                lastName = name.TryGetProperty("surname", out var ln)
                            ? ln.GetString()! : string.Empty;
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

    private static async Task<string> GetAccessTokenAsync(
        HttpClient http, string baseUrl, string clientId, string secret)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{clientId}:{secret}"));

        var req = new HttpRequestMessage(
            HttpMethod.Post, $"{baseUrl}/v1/oauth2/token")
        {
            Content = new StringContent(
                "grant_type=client_credentials",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var res = await http.SendAsync(req);
        var token = System.Text.Json.JsonSerializer.Deserialize<OAuthResponse>(
            await res.Content.ReadAsStringAsync(), JsonOptions)!;

        return token.AccessToken;
    }
}