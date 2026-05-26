namespace Keyspeech.FunctionApp.Configuration;

public class PayPalConfiguration
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string WebhookId { get; init; } = string.Empty;
    public bool IsSandbox { get; init; }
    public string SandboxUrl { get; init; } = string.Empty;
    public string ProductionUrl { get; init; } = string.Empty;
    public string Price { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string ReturnUrl { get; init; } = string.Empty;

    public string BaseUrl => IsSandbox ? SandboxUrl : ProductionUrl;

    public static PayPalConfiguration FromEnvironment()
    {
        return new PayPalConfiguration
        {
            ClientId = GetRequiredEnv("PAYPAL_CLIENT_ID"),
            ClientSecret = GetRequiredEnv("PAYPAL_CLIENT_SECRET"),
            WebhookId = GetRequiredEnv("PAYPAL_WEBHOOK_ID"),
            IsSandbox = Environment.GetEnvironmentVariable("PAYPAL_SANDBOX") != "false",
            SandboxUrl = GetRequiredEnv("PAYPAL_SANDBOX_URL"),
            ProductionUrl = GetRequiredEnv("PAYPAL_PRODUCTION_URL"),
            Price = GetRequiredEnv("PAYPAL_PRICE"),
            Currency = GetRequiredEnv("PAYPAL_CURRENCY"),
            ReturnUrl = Environment.GetEnvironmentVariable("PAYPAL_RETURN_URL") 
                ?? "https://keyspeech-eastus-1.azurewebsites.net/api/checkout/capture"
        };
    }

    private static string GetRequiredEnv(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable d'environnement manquante : {key}");
}
