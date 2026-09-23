using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.MemberSelection.Common;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;

namespace Vole_Papillon_Damour.Application.MemberSelection.Commands.MergeSelection;

public sealed class MergeSelectionCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<MergeSelectionCommand, ErrorOr<MergeSelectionResult>>
{
    public async Task<ErrorOr<MergeSelectionResult>> Handle(
        MergeSelectionCommand command,
        CancellationToken cancellationToken)
    {
        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId,
            command.Email,
            command.FirstName,
            command.LastName,
            cancellationToken);
        var now = dateTimeProvider.UtcNow;

        var existing = await dbContext.MemberSelectionItems
            .Where(item => item.UserId == user.Id)
            .Select(item => new { item.Isbn13, item.RareBookId })
            .ToListAsync(cancellationToken);
        var remoteKeys = existing
            .Select(item => item.Isbn13 is { } isbn
                ? $"edition:{isbn.Value}"
                : $"rare:{item.RareBookId!.Value}")
            .ToHashSet(StringComparer.Ordinal);
        var seenInBatch = new HashSet<string>(StringComparer.Ordinal);
        var remaining = MemberSelectionItem.MaxItemsPerMember - existing.Count;

        var added = 0;
        var alreadyPresent = 0;
        var rejected = new List<string>();

        foreach (var entry in command.Entries)
        {
            var reference = entry.Isbn13 ?? entry.RareBookId?.ToString() ?? string.Empty;
            var target = await SelectionTargetResolver.ResolveAsync(
                dbContext,
                entry.Isbn13,
                entry.RareBookId,
                cancellationToken);
            if (target.IsError)
            {
                rejected.Add(reference);
                continue;
            }

            var key = target.Value.Isbn13 is { } isbn
                ? $"edition:{isbn.Value}"
                : $"rare:{target.Value.RareBookId!.Value}";
            if (!seenInBatch.Add(key))
            {
                continue;
            }

            if (remoteKeys.Contains(key))
            {
                alreadyPresent++;
                continue;
            }

            if (remaining <= 0)
            {
                rejected.Add(reference);
                continue;
            }

            var addedAt = entry.AddedAt.Kind == DateTimeKind.Utc && entry.AddedAt <= now
                ? entry.AddedAt
                : now;
            var item = target.Value.Isbn13 is { } isbn13
                ? MemberSelectionItem.CreateForEdition(Guid.NewGuid(), user.Id, isbn13, addedAt)
                : MemberSelectionItem.CreateForRareBook(
                    Guid.NewGuid(), user.Id, target.Value.RareBookId!, addedAt);
            dbContext.MemberSelectionItems.Add(item);
            added++;
            remaining--;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new MergeSelectionResult(added, alreadyPresent, rejected);
    }
}
