using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.MemberSelection.Common;

namespace Vole_Papillon_Damour.Application.MemberSelection.Queries.GetMySelection;

public sealed record GetMySelectionQuery(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName) : IRequest<ErrorOr<MySelectionResult>>;
