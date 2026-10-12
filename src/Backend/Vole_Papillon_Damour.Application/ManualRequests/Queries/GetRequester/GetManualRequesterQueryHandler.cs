using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetQueue;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetRequester;

public sealed class GetManualRequesterQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetManualRequesterQuery, ErrorOr<ManualRequesterDetailResult>>
{
    public async Task<ErrorOr<ManualRequesterDetailResult>> Handle(
        GetManualRequesterQuery query,
        CancellationToken cancellationToken)
    {
        var requester = await dbContext.ManualRequesters
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == query.RequesterId, cancellationToken);
        if (requester is null)
        {
            return Errors.ManualRequest.RequesterNotFound(query.RequesterId);
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        var requests = await dbContext.ManualBookRequests
            .AsNoTracking()
            .Where(candidate => candidate.RequesterId == requester.Id)
            .OrderByDescending(candidate => candidate.RequestedAt)
            .ThenBy(candidate => candidate.Id)
            .ToListAsync(cancellationToken);
        var items = await ManualRequestQueueProjection.BuildAsync(
            dbContext,
            requests,
            new Dictionary<Guid, ManualRequester> { [requester.Id] = requester },
            now,
            cancellationToken);
        var consentRecorder = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == requester.ConsentRecordedBy)
            .Select(user => user.Name == null ? null : user.Name.FirstName)
            .SingleOrDefaultAsync(cancellationToken);

        return new ManualRequesterDetailResult(
            requester.Id,
            requester.IsErased ? null : requester.LastName,
            requester.IsErased ? null : requester.FirstName,
            requester.IsErased ? null : requester.Phone,
            requester.IsErased,
            requester.ConsentRecordedAt,
            consentRecorder,
            items);
    }
}
