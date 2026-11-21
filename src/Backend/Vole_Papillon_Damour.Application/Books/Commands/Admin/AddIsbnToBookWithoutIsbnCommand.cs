using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

// Ajoute un ISBN à une fiche à code interne (RG-174) : le code interne reste valide et résout vers la fiche ISBN.
public sealed record AddIsbnToBookWithoutIsbnCommand(
    string InternalCode,
    string Isbn13,
    UserId UpdatedBy) : IRequest<ErrorOr<AdminBookOperationResult>>;
