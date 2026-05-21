namespace Keyspeech.FunctionApp.Models;

/// <summary>Détails d'une capture PayPal confirmée.</summary>
public record CaptureDetails(
    string CaptureId,
    string Amount,
    string Currency,
    string CapturedAt);