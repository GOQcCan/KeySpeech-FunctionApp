using Keyspeech.FunctionApp.Models;
using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Keyspeech.FunctionApp.Services;

public class PayPalCheckoutService(
    IHttpClientFactory httpClientFactory,
    ILogger<PayPalCheckoutService> logger) : IPayPalCheckoutService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,                          // pour la désérialisation
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,      // pour la sérialisation (.NET 8+)
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<PayPalOrderResult> CheckoutOrdersAsync(string hardwareId)
    {
        // ✅ Tous les settings viennent d'Azure App Configuration
        string clientId = Env("PAYPAL_CLIENT_ID")!;
        string secret = Env("PAYPAL_CLIENT_SECRET")!;
        string price = Env("PAYPAL_PRICE")!;
        string currency = Env("PAYPAL_CURRENCY")!;
        HttpClient http = httpClientFactory.CreateClient("PayPal");
        // Sandbox ou Production selon l'environnement
        bool isSandbox = Env("PAYPAL_SANDBOX") != "false";
        string baseUrl = isSandbox
            ? Env("PAYPAL_SANDBOX_URL")!
            : Env("PAYPAL_PRODUCTION_URL")!;
        string accessToken = await GetAccessTokenAsync(http, baseUrl, clientId, secret);
        var payload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    amount = new { currency_code = currency, value = price },
                    description = "Licence KeySpeech",
                    custom_id = hardwareId
                }
            },
            application_context = new
            {
                brand_name = "KeySpeech",
                user_action = "PAY_NOW",
                return_url = "https://keyspeech-eastus-1.azurewebsites.net/api/checkout/capture"
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v2/checkout/orders")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("PayPal-Request-Id", Guid.NewGuid().ToString());

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var order = JsonSerializer.Deserialize<PayPalOrderResponse>(
            await response.Content.ReadAsStringAsync(), JsonOptions)!;

        var approvalUrl = order.Links.FirstOrDefault(l => l.Rel == "approve")?.Href
                          ?? throw new InvalidOperationException("Lien approve manquant");

        logger.LogInformation("Commande créée : {OrderId}", order.Id);

        return new PayPalOrderResult { OrderId = order.Id, ApprovalUrl = approvalUrl };
    }

    public async Task<PayPalCaptureResult> CaptureOrderAsync(string orderId)
    {
        HttpClient http = httpClientFactory.CreateClient("PayPal");
        bool isSandbox = Env("PAYPAL_SANDBOX") != "false";
        string baseUrl = isSandbox ? Env("PAYPAL_SANDBOX_URL")! : Env("PAYPAL_PRODUCTION_URL")!;
        string accessToken = await GetAccessTokenAsync(http, baseUrl,
            Env("PAYPAL_CLIENT_ID")!, Env("PAYPAL_CLIENT_SECRET")!);

        var request = new HttpRequestMessage(
        HttpMethod.Post,
        $"{baseUrl}/v2/checkout/orders/{orderId}/capture")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var order = JsonSerializer.Deserialize<CaptureOrderResponse>(
        await response.Content.ReadAsStringAsync(), JsonOptions)!;

        PurchaseUnit? unit = order.PurchaseUnits?.FirstOrDefault();
        OrdersCapture? capture = unit?.Payments?.Captures?.FirstOrDefault();

        return new PayPalCaptureResult
        {
            OrderId = order.Id ?? string.Empty,
            Status = order.Status ?? string.Empty,
            HardwareId = unit?.CustomId ?? string.Empty,
            CaptureId = capture?.Id ?? string.Empty,
            Amount = capture?.Amount?.MValue ?? string.Empty,
            Currency = capture?.Amount?.CurrencyCode ?? string.Empty
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
        var token = JsonSerializer.Deserialize<OAuthResponse>(
            await res.Content.ReadAsStringAsync(), JsonOptions)!;

        return token.AccessToken;
    }

    /// <summary>Lit une variable d'environnement obligatoire.</summary>
    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException(
            $"Variable d'environnement manquante : {key}");
}