using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;

namespace Vole_Papillon_Damour.Application.Actuality.Queries.GetDraftActualities;

public sealed record GetDraftActualitiesQuery : IRequest<ErrorOr<List<ActualityResult>>>;
