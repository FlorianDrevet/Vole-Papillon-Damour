using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetAssociationSettings;

public sealed record GetAssociationSettingsQuery : IRequest<ErrorOr<AssociationSettingsResult>>;
