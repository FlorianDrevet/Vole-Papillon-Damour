using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;

namespace Vole_Papillon_Damour.Application.Actuality.Queries;

public record GetLatestActualityQuery(
    
) : IRequest<ErrorOr<List<ActualityResult>>>;