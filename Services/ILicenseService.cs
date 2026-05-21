namespace Keyspeech.FunctionApp.Services;

public interface ILicenseService
{
    byte[] GenerateLicense(string hardwareID);
}