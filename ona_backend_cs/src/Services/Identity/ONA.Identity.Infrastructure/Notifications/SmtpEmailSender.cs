using System.Net.Mail;
using ONA.Identity.Application.Abstractions;

namespace ONA.Identity.Infrastructure.Notifications;

/// <summary>Sends mail through the SMTP server in Smtp:Host / Smtp:Port, from Smtp:From.</summary>
internal sealed class SmtpEmailSender(string host, int port, string from) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(host, port);
        using var message = new MailMessage(from, to, subject, body);
        await client.SendMailAsync(message, cancellationToken);
    }
}
