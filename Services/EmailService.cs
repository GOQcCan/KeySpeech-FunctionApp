using System.Net;
using System.Net.Mail;
using System.Text;

namespace Keyspeech.PayPal.Services;

public class EmailService() : IEmailService
{
    public async Task SendLicenseAsync(
        string email, string fullName, byte[] licenseKey)
    {
        string userName = Env("GMAIL_ADDRESS");
        using MailMessage msg = new();
        using SmtpClient smtpClient = new("smtp.gmail.com")
        {
            Port = 587,
            Credentials = new NetworkCredential(
                userName,
                Env("GMAIL_APP_PASSWORD")
            ),
            EnableSsl = true,
        };
        StringBuilder body = new();

        body.Append("Dear Customer,<BR /><BR />");

        using MemoryStream ms = new(licenseKey);
        Attachment data = new(ms, "keyspeech.license");
        msg.Attachments.Add(data);
        msg.Subject = "KeySpeech Full license file";
        body.Append("Thank you to buy KeySpeech License.<br /><br />This is your Full license file. Please move the license file in the same location of keyspeech.exe<br /><br />");

        body.Append("Best regards,<br /><br />KeySpeech Support");

        msg.From = new MailAddress(userName);
        msg.Bcc.Add(userName);
        msg.To.Add(email);
        msg.IsBodyHtml = true;
        msg.Body = body.ToString();

        await smtpClient.SendMailAsync(msg);
    }

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException(
            $"Variable d'environnement manquante : {key}");
}