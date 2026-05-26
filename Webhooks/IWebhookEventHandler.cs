using Keyspeech.FunctionApp.Models;

namespace Keyspeech.FunctionApp.Webhooks;

public interface IWebhookEventHandler
{
    string EventType { get; }
    Task<bool> CanHandleAsync(PayPalEvent evt);
    Task HandleAsync(PayPalEvent evt);
}
