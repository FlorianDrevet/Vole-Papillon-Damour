using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.MailingList.Commands.DeleteFromList;

public record DeleteFromMailingListCommand(
    string Email
) : IRequest<ErrorOr<bool>>;