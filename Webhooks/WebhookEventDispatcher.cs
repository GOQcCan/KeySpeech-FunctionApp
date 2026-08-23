using Keyspeech.FunctionApp.Models;
using Microsoft.Extensions.Logging;

namespace Keyspeech.FunctionApp.Webhooks;

public partial class WebhookEventDispatcher(
    IEnumerable<IWebhookEventHandler> handlers,
    ILogger<WebhookEventDispatcher> logger) : IWebhookEventDispatcher
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Traitement de l'événement {EventType} avec {Handler}")]
    private partial void LogHandlingEvent(string eventType, string handler);

    [LoggerMessage(Level = LogLevel.Information, Message = "Aucun handler pour l'événement : {EventType}")]
    private partial void LogNoHandlerFound(string eventType);

    public async Task DispatchAsync(PayPalEvent evt)
    {
        foreach (var handler in handlers)
        {
            if (await handler.CanHandleAsync(evt))
            {
                LogHandlingEvent(evt.EventType, handler.GetType().Name);

                await handler.HandleAsync(evt);
                return;
            }
        }

        LogNoHandlerFound(evt.EventType);
    }
}
