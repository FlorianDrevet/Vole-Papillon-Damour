using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

// Création d'un livre ordinaire sans ISBN (RG-170, RG-180) : la plateforme attribue le code interne.
public sealed record CreateBookWithoutIsbnCommand(
    string Title,
    int QuantityAvailable,
    UserId AddedBy,
    string? Authors = null,
    string? Publisher = null,
    int? PublicationYear = null,
    string? PhysicalFormat = null,
    string? Language = null,
    string? Genre = null,
    string? Summary = null,
    string? Edition = null,
    int? Pages = null) : IRequest<ErrorOr<AdminBookOperationResult>>;