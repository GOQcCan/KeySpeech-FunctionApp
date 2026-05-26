using Keyspeech.FunctionApp.Configuration;
using Keyspeech.FunctionApp.Models;
using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Keyspeech.FunctionApp.Services;

public class PayPalWebhookService(
    HttpClient httpClient,
    ILogger<PayPalWebhookService> logger,
    PayPalConfiguration config,
    PaypalServerSdkClient paypalClient) : IPayPalWebhookService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<bool> ValidateSignatureAsync(
        IReadOnlyDictionary<string, string> headers,
        string rawBody)
    {
        try
        {
            string accessToken = await GetAccessTokenAsync();

            var payload = new
            {
                auth_algo = H(headers, "paypal-auth-algo"),
                cert_url = H(headers, "paypal-cert-url"),
                transmission_id = H(headers, "paypal-transmission-id"),
                transmission_sig = H(headers, "paypal-transmission-sig"),
                transmission_time = H(headers, "paypal-transmission-time"),
                webhook_id = config.WebhookId,
                webhook_event = JsonSerializer.Deserialize<JsonElement>(rawBody, JsonOptions)
            };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{config.BaseUrl}/v1/notifications/verify-webhook-signature")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload, JsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                logger.LogError(
                    "Erreur PayPal verify-webhook-signature {Status}: {Body}",
                    response.StatusCode, error);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<VerifySignatureResponse>(json, JsonOptions);

            logger.LogInformation(
                "Résultat validation signature : {Status}",
                result?.VerificationStatus);

            return result?.VerificationStatus == "SUCCESS";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception lors de la validation de signature PayPal");
            return false;
        }
    }

    public async Task<CaptureDetails?> GetVerifiedCaptureAsync(string captureId)
    {
        try
        {
            logger.LogInformation(
                "Vérification capture {CaptureId} via SDK officiel", captureId);

            var input = new GetCapturedPaymentInput
            {
                CaptureId = captureId
            };

            var apiResponse = await paypalClient.PaymentsController
                .GetCapturedPaymentAsync(input);

            var capture = apiResponse.Data;

            logger.LogInformation(
                "Capture {Id} — statut : {Status} — montant : {Amount} {Currency}",
                capture.Id,
                capture.Status,
                capture.Amount?.MValue,
                capture.Amount?.CurrencyCode);

            if (capture.Status != CaptureStatus.Completed)
            {
                logger.LogWarning(
                    "Capture {Id} ignorée — statut : {Status}",
                    capture.Id, capture.Status);
                return null;
            }

            return new CaptureDetails(
                CaptureId: capture.Id!,
                Amount: capture.Amount?.MValue ?? "0",
                Currency: capture.Amount?.CurrencyCode ?? "CAD",
                CapturedAt: capture.CreateTime ?? DateTime.UtcNow.ToString("o")
            );
        }
        catch (ApiException ex)
        {
            logger.LogError(ex,
                "Erreur SDK PayPal capture {Id}: HTTP {Status} — {Message}",
                captureId, ex.ResponseCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Exception inattendue vérification capture {Id}", captureId);
            return null;
        }
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{config.ClientId}:{config.ClientSecret}"));

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{config.BaseUrl}/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            ])
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Impossible d'obtenir le token PayPal ({response.StatusCode}): {body}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<OAuthTokenResponse>(json, JsonOptions);

        return token?.AccessToken
            ?? throw new InvalidOperationException("Token PayPal vide ou invalide");
    }

    private static string H(IReadOnlyDictionary<string, string> headers, string key) =>
        headers.TryGetValue(key, out var val) ? val : string.Empty;
}