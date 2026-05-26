using Keyspeech.FunctionApp.Models;
using Microsoft.Extensions.Logging;

namespace Keyspeech.FunctionApp.Webhooks;

public class WebhookEventDispatcher(
    IEnumerable<IWebhookEventHandler> handlers,
    ILogger<WebhookEventDispatcher> logger) : IWebhookEventDispatcher
{
    public async Task DispatchAsync(PayPalEvent evt)
    {
        foreach (var handler in handlers)
        {
            if (await handler.CanHandleAsync(evt))
            {
                logger.LogInformation(
                    "Traitement de l'événement {EventType} avec {Handler}",
                    evt.EventType,
                    handler.GetType().Name);

                await handler.HandleAsync(evt);
                return;
            }
        }

        logger.LogInformation("Aucun handler pour l'événement : {EventType}", evt.EventType);
    }
}
