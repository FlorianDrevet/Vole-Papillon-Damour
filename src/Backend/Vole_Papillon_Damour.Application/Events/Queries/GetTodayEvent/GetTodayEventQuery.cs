using MediatR;
using Vole_Papillon_Damour.Application.Events.Common;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetTodayEvent;

/// <summary>The event happening today in Paris, a loto first; <c>null</c> when there is none.</summary>
public record GetTodayEventQuery : IRequest<TodayEventResult?>;
