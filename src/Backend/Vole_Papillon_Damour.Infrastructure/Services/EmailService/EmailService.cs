using Azure;
using Azure.Communication.Email;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.EmailService;

public class EmailService: IEmailService
{
    private readonly EmailClient _emailClient;
    private readonly ITableStorageService _tableStorageService;
    private const string _sender  = "DoNotReply@notifications.volepapillondamour.fr";

    public EmailService(EmailClient emailClient, ITableStorageService tableStorageService)
    {
        _emailClient = emailClient;
        _tableStorageService = tableStorageService;
    }

    public async Task SendEmailAsync(string email, string subject, string message, CancellationToken cancellationToken)
    {
        await _emailClient.SendAsync(
            Azure.WaitUntil.Started,
            _sender,
            email,
            subject,
            message, 
            cancellationToken: cancellationToken);
    }

    public async Task SendEmailToMailingListAsync(string subject, string message, CancellationToken cancellationToken)
    {
        var listMails = await _tableStorageService.GetMailingListAsync(cancellationToken);
        foreach (var mail in listMails)
        {
            await SendEmailAsync(mail, subject, message, cancellationToken);
        } 
    }
}