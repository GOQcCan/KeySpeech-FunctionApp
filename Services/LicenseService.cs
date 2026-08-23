using Keyspeech.FunctionApp.Configuration;
using Microsoft.Extensions.Logging;

namespace Keyspeech.FunctionApp.Services;

public partial class LicenseService(ILogger<LicenseService> logger, LicenseConfiguration config) : ILicenseService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Génération de licence pour {HardwareID}")]
    private partial void LogGeneratingLicense(string hardwareID);

    public byte[] GenerateLicense(string hardwareID)
    {
        LogGeneratingLicense(hardwareID);

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