using Keyspeech.FunctionApp.Configuration;
using Microsoft.Extensions.Logging;

namespace Keyspeech.FunctionApp.Services;

public class LicenseService(ILogger<LicenseService> logger, LicenseConfiguration config) : ILicenseService
{
    public byte[] GenerateLicense(string hardwareID)
    {
        logger.LogInformation("Génération de licence pour {HardwareID}", hardwareID);

        LicenseGenerator licensegen = new();
        licensegen.LoadMasterKeyFromString(config.MasterKey);
        licensegen.HardwareID_Board = config.HardwareIdBoard;
        licensegen.HardwareID_HDD = config.HardwareIdHdd;
        licensegen.HardwareID_MAC = config.HardwareIdMac;
        licensegen.HardwareID_CPU = config.HardwareIdCpu;
        licensegen.Hardware_Enabled = config.HardwareEnabled;
        licensegen.HardwareID = hardwareID;
        licensegen.Individual_Licensing_Behaviour = config.IndividualLicensingBehaviour;
        licensegen.AddAdditonalLicenseInformation("LicenseType", config.LicenseType);

        return licensegen.CreateLicenseFile();
    }
}