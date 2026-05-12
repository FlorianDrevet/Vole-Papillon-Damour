using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.MailingList.Commands;

public class AddToMailingListCommandHandler(ITableStorageService tableStorageService)
    : IRequestHandler<AddToMailingListCommand, ErrorOr<bool>>
{
    public async Task<ErrorOr<bool>> Handle(AddToMailingListCommand command, CancellationToken cancellationToken)
    {
        var isEmailInMailingList = await tableStorageService.IsEmailInMailingListAsync(command.Email, cancellationToken);
        if (isEmailInMailingList)
        {
            return Errors.MailingList.EmailAlreadyExists(command.Email);
        }
        
        var isSuccess = await tableStorageService.AddMailToNewsletterAsync(command.Email, cancellationToken);
        
        if (!isSuccess)
        {
            return Errors.MailingList.ErrorWhileAddingEmail(command.Email);
        }

        return true;
    }
}