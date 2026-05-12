using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.MailingList;

public record AddToMailingListCommand(
    string Email
) : IRequest<ErrorOr<bool>>;