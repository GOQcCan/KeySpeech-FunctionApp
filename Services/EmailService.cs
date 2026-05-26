using Keyspeech.FunctionApp.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Keyspeech.FunctionApp.Services;

public class EmailService(EmailConfiguration config) : IEmailService
{
    public async Task SendLicenseAsync(
        string email, string fullName, byte[] licenseKey)
    {
        using MailMessage msg = new();
        using SmtpClient smtpClient = new(config.SmtpHost)
        {
            Port = config.SmtpPort,
            Credentials = new NetworkCredential(
                config.SenderAddress,
                config.SenderPassword
            ),
            EnableSsl = config.EnableSsl,
        };
        StringBuilder body = new();

        body.Append("Dear Customer,<BR /><BR />");

        using MemoryStream ms = new(licenseKey);
        Attachment data = new(ms, "keyspeech.license");
        msg.Attachments.Add(data);
        msg.Subject = config.SubjectTemplate;
        body.Append("Thank you to buy KeySpeech License.<br /><br />This is your Full license file. Please move the license file in the same location of keyspeech.exe<br /><br />");

        body.Append("Best regards,<br /><br />KeySpeech Support");

        msg.From = new MailAddress(config.SenderAddress);
        msg.Bcc.Add(config.SenderAddress);
        msg.To.Add(email);
        msg.IsBodyHtml = true;
        msg.Body = body.ToString();

        await smtpClient.SendMailAsync(msg);
    }
}