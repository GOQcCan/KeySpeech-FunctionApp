namespace Keyspeech.FunctionApp.Configuration;

public class EmailConfiguration
{
    public string SmtpHost { get; init; } = "smtp.gmail.com";
    public int SmtpPort { get; init; } = 587;
    public string SenderAddress { get; init; } = string.Empty;
    public string SenderPassword { get; init; } = string.Empty;
    public bool EnableSsl { get; init; } = true;
    public string SubjectTemplate { get; init; } = "KeySpeech Full license file";

    public static EmailConfiguration FromEnvironment()
    {
        return new EmailConfiguration
        {
            SmtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.gmail.com",
            SmtpPort = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out int port) ? port : 587,
            SenderAddress = GetRequiredEnv("GMAIL_ADDRESS"),
            SenderPassword = GetRequiredEnv("GMAIL_APP_PASSWORD"),
            EnableSsl = Environment.GetEnvironmentVariable("SMTP_ENABLE_SSL") != "false",
            SubjectTemplate = Environment.GetEnvironmentVariable("EMAIL_SUBJECT") ?? "KeySpeech Full license file"
        };
    }

    private static string GetRequiredEnv(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable d'environnement manquante : {key}");
}
