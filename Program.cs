using Keyspeech.FunctionApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;
using static System.Net.Mime.MediaTypeNames;
using Environment = System.Environment;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Enregistrement du client PayPalServerSDK officiel
        services.AddSingleton<PaypalServerSdkClient>(sp =>
        {
            var clientId = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID")!;
            var clientSecret = Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET")!;

            return new PaypalServerSdkClient.Builder()
                .ClientCredentialsAuth(
                    new ClientCredentialsAuthModel.Builder(clientId, clientSecret)
                        .Build())
                // Sandbox pour les tests
                // Production : PaypalServerSdk.Standard.Environment.Production
                .Environment(PaypalServerSdk.Standard.Environment.Sandbox)
                .Build();
        });
        services.AddHttpClient("PayPal");
        services.AddSingleton<IPayPalCheckoutService, PayPalCheckoutService>();
        services.AddHttpClient<IPayPalWebhookService, PayPalWebhookService>();
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<IEmailService, EmailService>();
    })
    .Build();

await host.RunAsync();