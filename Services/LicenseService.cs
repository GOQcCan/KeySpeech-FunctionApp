using Microsoft.Extensions.Logging;

namespace Keyspeech.FunctionApp.Services;

public class LicenseService(ILogger<LicenseService> logger) : ILicenseService
{
    private readonly ILogger<LicenseService> _logger = logger;

    public byte[] GenerateLicense(string hardwareID)
    {
        _logger.LogInformation("Génération de licence pour {HardwareID}", hardwareID);

        LicenseGenerator licensegen = new();
        licensegen.LoadMasterKeyFromString(Env("LICENSE_GENERATOR_MASTERKEY"));
        licensegen.HardwareID_Board = true;
        licensegen.HardwareID_HDD = false;
        licensegen.HardwareID_MAC = false;
        licensegen.HardwareID_CPU = true;
        licensegen.Hardware_Enabled = true;
        licensegen.HardwareID = hardwareID;
        licensegen.Individual_Licensing_Behaviour = true;
        licensegen.Evaluation_Enabled = false;
        licensegen.AddAdditonalLicenseInformation("LicenseType", "Full");

        return licensegen.CreateLicenseFile();
    }

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException(
            $"Variable d'environnement manquante : {key}");
}