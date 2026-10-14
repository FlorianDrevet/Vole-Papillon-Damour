using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetQueue;

public enum ManualRequestTab : byte
{
    Found,
    Searching,
    Closed
}

public sealed record GetManualRequestQueueQuery(
    ManualRequestTab Tab,
    string? Search,
    int Page,
    int PageSize) : IRequest<ErrorOr<ManualRequestQueueResult>>;
