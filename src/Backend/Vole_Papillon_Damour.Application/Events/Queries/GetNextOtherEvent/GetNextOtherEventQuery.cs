using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Events.Common;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetNextBooks;

public record GetNextOtherEventQuery(
    
) : IRequest<ErrorOr<List<AssoEventResult>>>;