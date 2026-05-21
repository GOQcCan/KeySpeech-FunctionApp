using Keyspeech.FunctionApp.Models;
using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Controllers;
using PaypalServerSdk.Standard.Exceptions;
using PaypalServerSdk.Standard.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Environment = System.Environment;

namespace Keyspeech.FunctionApp.Services;

public class PayPalWebhookService : IPayPalWebhookService
{
    // ------------------------------------------------------------------ //
    //  Dépendances
    // ------------------------------------------------------------------ //
    private readonly HttpClient _httpClient;
    private readonly ILogger<PayPalWebhookService> _logger;

    // Contrôleur Payments du SDK officiel PayPalServerSDK
    private readonly PaymentsController _paymentsController;

    // ------------------------------------------------------------------ //
    //  Configuration (lue depuis Azure App Settings)
    // ------------------------------------------------------------------ //
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _webhookId;
    private readonly string _baseUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,           // désérialisation robuste
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,  // sérialisation PayPal (.NET 8+)
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ------------------------------------------------------------------ //
    //  Constructeur
    // ------------------------------------------------------------------ //
    public PayPalWebhookService(
        HttpClient httpClient,
        ILogger<PayPalWebhookService> logger,
        PaypalServerSdkClient paypalClient)
    {
        _httpClient = httpClient;
        _logger = logger;

        _clientId = Env("PAYPAL_CLIENT_ID");
        _clientSecret = Env("PAYPAL_CLIENT_SECRET");
        _webhookId = Env("PAYPAL_WEBHOOK_ID");

        // Sandbox ou Production selon l'environnement
        bool isSandbox = Env("PAYPAL_SANDBOX") != "false";
        _baseUrl = isSandbox
            ? Env("PAYPAL_SANDBOX_URL")!
            : Env("PAYPAL_PRODUCTION_URL")!;

        // Récupère le contrôleur Payments depuis le client injecté
        _paymentsController = paypalClient.PaymentsController;
    }

    // ================================================================== //
    //  1. VALIDATION DE SIGNATURE
    //     PayPal signe chaque webhook — on envoie les headers + body
    //     à l'endpoint /v1/notifications/verify-webhook-signature
    //     qui répond SUCCESS ou FAILURE.
    // ================================================================== //
    public async Task<bool> ValidateSignatureAsync(
        IReadOnlyDictionary<string, string> headers,
        string rawBody)
    {
        try
        {
            string accessToken = await GetAccessTokenAsync();

            // Corps de la requête de vérification
            var payload = new
            {
                auth_algo = H(headers, "paypal-auth-algo"),
                cert_url = H(headers, "paypal-cert-url"),
                transmission_id = H(headers, "paypal-transmission-id"),
                transmission_sig = H(headers, "paypal-transmission-sig"),
                transmission_time = H(headers, "paypal-transmission-time"),
                webhook_id = _webhookId,
                // Le body brut doit être désérialisé en JsonElement
                // pour que PayPal le reçoive comme objet JSON, pas comme string
                webhook_event = JsonSerializer.Deserialize<JsonElement>(rawBody, JsonOptions)
            };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_baseUrl}/v1/notifications/verify-webhook-signature")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload, JsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Erreur PayPal verify-webhook-signature {Status}: {Body}",
                    response.StatusCode, error);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<VerifySignatureResponse>(json, JsonOptions);

            _logger.LogInformation(
                "Résultat validation signature : {Status}",
                result?.VerificationStatus);

            return result?.VerificationStatus == "SUCCESS";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de la validation de signature PayPal");
            return false;
        }
    }

    // ================================================================== //
    //  2. VÉRIFICATION DE LA CAPTURE VIA LE SDK OFFICIEL
    //     Après avoir validé la signature, on interroge PayPal
    //     indépendamment pour confirmer que la capture est COMPLETED.
    //     Cela protège contre les replays et les faux événements.
    // ================================================================== //
    public async Task<CaptureDetails?> GetVerifiedCaptureAsync(string captureId)
    {
        try
        {
            _logger.LogInformation(
                "Vérification capture {CaptureId} via SDK officiel", captureId);

            // Utilisation de la bonne méthode du SDK
            var input = new GetCapturedPaymentInput
            {
                CaptureId = captureId
            };

            var apiResponse = await _paymentsController
                .GetCapturedPaymentAsync(input);

            var capture = apiResponse.Data;

            _logger.LogInformation(
                "Capture {Id} — statut : {Status} — montant : {Amount} {Currency}",
                capture.Id,
                capture.Status,
                capture.Amount?.MValue,
                capture.Amount?.CurrencyCode);

            if (capture.Status != CaptureStatus.Completed)
            {
                _logger.LogWarning(
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
            _logger.LogError(ex,
                "Erreur SDK PayPal capture {Id}: HTTP {Status} — {Message}",
                captureId, ex.ResponseCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception inattendue vérification capture {Id}", captureId);
            return null;
        }
    }

    // ================================================================== //
    //  MÉTHODES PRIVÉES
    // ================================================================== //

    /// <summary>
    /// Obtient un token OAuth2 Bearer auprès de PayPal.
    /// Utilisé uniquement pour la validation de signature
    /// (le SDK gère son propre token en interne).
    /// </summary>
    private async Task<string> GetAccessTokenAsync()
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_baseUrl}/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            ])
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await _httpClient.SendAsync(request);

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

    /// <summary>Lit une variable d'environnement obligatoire.</summary>
    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException(
            $"Variable d'environnement manquante : {key}");

    /// <summary>Extrait un header de façon sécurisée (retourne string.Empty si absent).</summary>
    private static string H(IReadOnlyDictionary<string, string> headers, string key) =>
        headers.TryGetValue(key, out var val) ? val : string.Empty;
}