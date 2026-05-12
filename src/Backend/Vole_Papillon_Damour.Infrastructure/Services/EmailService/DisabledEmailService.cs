using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.EmailService;

/// <summary>
/// Prevents local startup from depending on Azure Communication Services when email secrets are not configured.
/// </summary>
public sealed class DisabledEmailService : IEmailService
{
    private readonly ILogger<DisabledEmailService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisabledEmailService"/> class.
    /// </summary>
    /// <param name="logger">Logs each no-op email attempt.</param>
    public DisabledEmailService(ILogger<DisabledEmailService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Logs and ignores a direct email send request when email is disabled locally.
    /// </summary>
    /// <param name="email">Identifies the email recipient.</param>
    /// <param name="subject">Provides the email subject.</param>
    /// <param name="message">Provides the email body.</param>
    /// <param name="cancellationToken">Propagates the cancellation signal.</param>
    /// <returns>A completed task.</returns>
    public Task SendEmailAsync(string email, string subject, string message, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Email is disabled for local development. Skipping email to {Email} with subject {Subject}.", email, subject);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs and ignores a mailing list broadcast when email is disabled locally.
    /// </summary>
    /// <param name="subject">Provides the email subject.</param>
    /// <param name="message">Provides the email body.</param>
    /// <param name="cancellationToken">Propagates the cancellation signal.</param>
    /// <returns>A completed task.</returns>
    public Task SendEmailToMailingListAsync(string subject, string message, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Email is disabled for local development. Skipping mailing list email with subject {Subject}.", subject);
        return Task.CompletedTask;
    }
}