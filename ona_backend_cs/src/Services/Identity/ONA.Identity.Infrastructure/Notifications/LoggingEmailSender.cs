using Microsoft.Extensions.Logging;
using ONA.Identity.Application.Abstractions;

namespace ONA.Identity.Infrastructure.Notifications;

/// <summary>
/// DEVELOPMENT ONLY: used when Smtp:Host is not configured. Logs the whole message (including the
/// reset token) so it can be copied from the console. Never use in production.
/// </summary>
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("EMAIL (not sent, no SMTP configured) to {To} | {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
