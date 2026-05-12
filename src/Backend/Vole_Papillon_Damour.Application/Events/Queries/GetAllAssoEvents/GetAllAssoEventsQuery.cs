using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Events.Common;

namespace Vole_Papillon_Damour.Application.Events.Queries;

public record GetAllAssoEventsQuery(
    
) : IRequest<ErrorOr<List<AssoEventResult>>>;