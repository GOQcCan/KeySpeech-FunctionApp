namespace Keyspeech.FunctionApp.Services;

public interface IEmailService
{
    Task SendLicenseAsync(string email, string fullName, byte[] licenseKey);
}