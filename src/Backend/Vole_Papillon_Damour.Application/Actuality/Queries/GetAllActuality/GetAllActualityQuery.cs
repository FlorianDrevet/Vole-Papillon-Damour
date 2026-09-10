using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;

namespace Vole_Papillon_Damour.Application.Actuality.Queries.GetAllActuality;

public record GetAllActualityQuery(
    bool IncludeDrafts = false
) : IRequest<ErrorOr<List<ActualityResult>>>;
