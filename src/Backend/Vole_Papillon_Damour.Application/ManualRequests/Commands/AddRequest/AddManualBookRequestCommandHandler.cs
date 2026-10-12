using System.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.AddRequest;

public sealed class AddManualBookRequestCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddManualBookRequestCommand, ErrorOr<ManualBookRequestCreatedResult>>
{
    public async Task<ErrorOr<ManualBookRequestCreatedResult>> Handle(
        AddManualBookRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (command.By is null || command.By.Value == Guid.Empty)
        {
            return Errors.ManualRequest.InvalidAuthor();
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("ManualRequest.InvalidClock", "The request timestamp must be expressed in UTC.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var requester = await dbContext.ManualRequesters
            .SingleOrDefaultAsync(candidate => candidate.Id == command.RequesterId, cancellationToken);
        if (requester is null)
        {
            return Errors.ManualRequest.RequesterNotFound(command.RequesterId);
        }

        if (requester.IsErased)
        {
            return Errors.ManualRequest.RequesterErased();
        }

        if (!TryCreateRequest(command, now, out var request))
        {
            return Errors.ManualRequest.InvalidTarget();
        }

        var openRequests = await dbContext.ManualBookRequests
            .AsNoTracking()
            .Where(existing => existing.RequesterId == requester.Id &&
                               (existing.Status == ManualBookRequestStatus.Searching ||
                                existing.Status == ManualBookRequestStatus.Found))
            .ToListAsync(cancellationToken);
        if (openRequests.Any(existing => HasSameTarget(existing, request)))
        {
            return Errors.ManualRequest.DuplicateRequest();
        }

        var settings = await dbContext.AssociationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == AssociationSettings.SingletonId,
                cancellationToken);
        var maximumOpenRequests = settings?.WatchlistMaxItems ?? 100;
        if (openRequests.Count >= maximumOpenRequests)
        {
            return Errors.ManualRequest.LimitReached(maximumOpenRequests);
        }

        if (command.PickFromStock)
        {
            var pick = await ManualRequestStockPick.PickAsync(
                dbContext, request, command.By, now, cancellationToken);
            if (pick.IsError)
            {
                return pick.Errors;
            }
        }

        dbContext.ManualBookRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ManualBookRequestCreatedResult(request.Id, request.Status);
    }

    private static bool TryCreateRequest(
        AddManualBookRequestCommand command,
        DateTime requestedAt,
        out ManualBookRequest request)
    {
        request = null!;
        try
        {
            if (command.Scope == ManualBookRequestScope.Work)
            {
                var workId = string.IsNullOrWhiteSpace(command.WorkId) ? null : command.WorkId.Trim();
                if (workId is null || workId.Length > ManualBookRequest.WorkIdMaxLength ||
                    !string.IsNullOrWhiteSpace(command.Isbn13))
                {
                    return false;
                }

                request = ManualBookRequest.CreateWork(
                    Guid.NewGuid(), command.RequesterId, workId,
                    command.Title, command.Authors, command.Publisher, command.PublicationYear, requestedAt);
                return true;
            }

            if (command.Scope == ManualBookRequestScope.Edition &&
                string.IsNullOrWhiteSpace(command.WorkId) &&
                Isbn13.TryCreate(command.Isbn13, out var isbn13))
            {
                request = ManualBookRequest.CreateEdition(
                    Guid.NewGuid(), command.RequesterId, isbn13,
                    command.Title, command.Authors, command.Publisher, command.PublicationYear, requestedAt);
                return true;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }

        return false;
    }

    private static bool HasSameTarget(ManualBookRequest existing, ManualBookRequest candidate) =>
        existing.Scope == candidate.Scope &&
        (candidate.Scope == ManualBookRequestScope.Work
            ? string.Equals(existing.WorkId, candidate.WorkId, StringComparison.Ordinal)
            : existing.Isbn13 == candidate.Isbn13);
}
