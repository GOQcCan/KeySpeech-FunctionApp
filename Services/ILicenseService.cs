namespace Keyspeech.PayPal.Services;

public interface ILicenseService
{
    byte[] GenerateLicense(string hardwareID);
}