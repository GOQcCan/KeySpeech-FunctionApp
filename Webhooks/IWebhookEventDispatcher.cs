using Keyspeech.FunctionApp.Models;

namespace Keyspeech.FunctionApp.Webhooks
{
    public interface IWebhookEventDispatcher
    {
        Task DispatchAsync(PayPalEvent evt);
    }
}
