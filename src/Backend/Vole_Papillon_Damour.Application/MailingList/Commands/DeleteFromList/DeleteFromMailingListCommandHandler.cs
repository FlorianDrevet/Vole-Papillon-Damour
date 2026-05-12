using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.MailingList.Commands.DeleteFromList;

public class DeleteFromMailingListCommandHandler(ITableStorageService tableStorageService)
    : IRequestHandler<DeleteFromMailingListCommand, ErrorOr<bool>>
{
    public async Task<ErrorOr<bool>> Handle(DeleteFromMailingListCommand command, CancellationToken cancellationToken)
    {
        var isEmailInMailingList = await tableStorageService.IsEmailInMailingListAsync(command.Email, cancellationToken);
        if (!isEmailInMailingList)
        {
            return Errors.MailingList.EmailDoesNotExist(command.Email);
        }
        
        var isSuccess = await tableStorageService.DeleteMailFromNewsletterAsync(command.Email, cancellationToken);
        if (!isSuccess)
        {
            return Errors.MailingList.ErrorWhileAddingEmail(command.Email);
        }

        return true;
    }
}