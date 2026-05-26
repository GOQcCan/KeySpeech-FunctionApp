namespace Keyspeech.FunctionApp.Configuration;

public class LicenseConfiguration
{
    public string MasterKey { get; init; } = string.Empty;
    public bool HardwareIdBoard { get; init; } = true;
    public bool HardwareIdHdd { get; init; } = false;
    public bool HardwareIdMac { get; init; } = false;
    public bool HardwareIdCpu { get; init; } = true;
    public bool HardwareEnabled { get; init; } = true;
    public bool IndividualLicensingBehaviour { get; init; } = true;
    public string LicenseType { get; init; } = "Full";

    public static LicenseConfiguration FromEnvironment()
    {
        return new LicenseConfiguration
        {
            MasterKey = GetRequiredEnv("LICENSE_GENERATOR_MASTERKEY"),
            HardwareIdBoard = GetBoolEnv("LICENSE_HARDWARE_ID_BOARD", true),
            HardwareIdHdd = GetBoolEnv("LICENSE_HARDWARE_ID_HDD", false),
            HardwareIdMac = GetBoolEnv("LICENSE_HARDWARE_ID_MAC", false),
            HardwareIdCpu = GetBoolEnv("LICENSE_HARDWARE_ID_CPU", true),
            HardwareEnabled = GetBoolEnv("LICENSE_HARDWARE_ENABLED", true),
            IndividualLicensingBehaviour = GetBoolEnv("LICENSE_INDIVIDUAL_BEHAVIOUR", true),
            LicenseType = Environment.GetEnvironmentVariable("LICENSE_TYPE") ?? "Full"
        };
    }

    private static string GetRequiredEnv(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable d'environnement manquante : {key}");

    private static bool GetBoolEnv(string key, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return value == null ? defaultValue : bool.Parse(value);
    }
}
