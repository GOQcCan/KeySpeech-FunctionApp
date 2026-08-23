using Keyspeech.FunctionApp.Configuration;
using Keyspeech.FunctionApp.Models;
using Keyspeech.FunctionApp.Services;
using Keyspeech.FunctionApp.Validation;
using Keyspeech.FunctionApp.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;
using Polly;
using Polly.Extensions.Http;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // ========== Configurations ==========
        var paypalConfig = PayPalConfiguration.FromEnvironment();
        var licenseConfig = LicenseConfiguration.FromEnvironment();
        var emailConfig = EmailConfiguration.FromEnvironment();

        services.AddSingleton(paypalConfig);
        services.AddSingleton(licenseConfig);
        services.AddSingleton(emailConfig);

        // ========== PayPal SDK Client ==========
        services.AddSingleton<PaypalServerSdkClient>(sp =>
        {
            return new PaypalServerSdkClient.Builder()
                .ClientCredentialsAuth(
                    new ClientCredentialsAuthModel.Builder(
                        paypalConfig.ClientId,
                        paypalConfig.ClientSecret)
                        .Build())
                .Environment(paypalConfig.IsSandbox
                    ? PaypalServerSdk.Standard.Environment.Sandbox
                    : PaypalServerSdk.Standard.Environment.Production)
                .Build();
        });

        // ========== Polly Retry Policy ==========
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        // ========== HTTP Clients avec Polly ==========
        services.AddHttpClient<IPayPalWebhookService, PayPalWebhookService>()
            .AddPolicyHandler(retryPolicy);

        // ========== Services Core ==========
        services.AddSingleton<IPayPalCheckoutService, PayPalCheckoutService>();
        services.AddSingleton<IPayPalOrderService, PayPalCheckoutService>();
        services.AddSingleton<IPayPalCaptureService, PayPalCheckoutService>();
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<IEmailService, EmailService>();

        // ========== Orchestration ==========
        services.AddSingleton<IOrderProcessingService, OrderProcessingService>();

        // ========== Parsing & Validation ==========
        services.AddSingleton<IPayPalEventParser, PayPalEventParser>();
        services.AddSingleton<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();

        // ========== Webhook Pattern Strategy ==========
        services.AddSingleton<IWebhookEventHandler, PaymentCaptureCompletedHandler>();
        services.AddSingleton<IWebhookEventDispatcher, WebhookEventDispatcher>();
    })
    .Build();

await host.RunAsync();
