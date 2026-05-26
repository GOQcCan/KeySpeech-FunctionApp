namespace Keyspeech.FunctionApp.Services;

public interface IOrderProcessingService
{
    Task ProcessPaymentCompletedAsync(string orderId, string captureId, string hardwareId);
}
