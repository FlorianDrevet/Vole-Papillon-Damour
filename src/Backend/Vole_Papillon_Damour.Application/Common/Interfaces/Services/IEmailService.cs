namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IEmailService
{
    Task SendEmailAsync(string email, string subject, string message, CancellationToken cancellationToken);
    Task SendEmailToMailingListAsync(string subject, string message, CancellationToken cancellationToken);
}