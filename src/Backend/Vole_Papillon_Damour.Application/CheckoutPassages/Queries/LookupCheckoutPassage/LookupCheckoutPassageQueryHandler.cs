using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.CheckoutPassages.Queries.LookupCheckoutPassage;

public sealed class LookupCheckoutPassageQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LookupCheckoutPassageQuery, ErrorOr<CheckoutPassageLookupResult>>
{
    private const int ShortReferenceLookbackDays = 90;

    public async Task<ErrorOr<CheckoutPassageLookupResult>> Handle(
        LookupCheckoutPassageQuery query,
        CancellationToken cancellationToken)
    {
        var reference = query.Reference.Trim();
        Guid? exactId = Guid.TryParse(reference, out var parsedId) ? parsedId : null;
        IQueryable<CheckoutPassage> candidates = dbContext.CheckoutPassages
            .AsNoTracking()
            .Where(passage => passage.Status == CheckoutPassageStatus.Associated);

        if (exactId is { } id)
        {
            candidates = candidates.Where(passage => passage.Id == id);
        }
        else
        {
            var cutoff = dateTimeProvider.UtcNow.AddDays(-ShortReferenceLookbackDays);
            var recent = await candidates
                .Where(passage => passage.OccurredAt >= cutoff)
                .Select(passage => new PassageCandidate(passage.Id, passage.OccurredAt, passage.UserId))
                .ToListAsync(cancellationToken);
            var matches = recent
                .Where(passage => passage.Id.ToString("N").StartsWith(reference, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return await BuildResultAsync(matches, cancellationToken);
        }

        var exactMatches = await candidates
            .Select(passage => new PassageCandidate(passage.Id, passage.OccurredAt, passage.UserId))
            .ToListAsync(cancellationToken);
        return await BuildResultAsync(exactMatches, cancellationToken);
    }

    private async Task<ErrorOr<CheckoutPassageLookupResult>> BuildResultAsync(
        IReadOnlyCollection<PassageCandidate> matches,
        CancellationToken cancellationToken)
    {
        if (matches.Count == 0)
        {
            return Error.NotFound(
                code: "CheckoutPassage.NotFound",
                description: "Aucun passage associé ne correspond à cette référence.");
        }

        if (matches.Count > 1)
        {
            return Errors.CheckoutPassage.AmbiguousReference();
        }

        var match = matches.Single();
        var lineCount = await dbContext.CheckoutPassageLines.CountAsync(
            line => line.CheckoutPassageId == match.Id,
            cancellationToken);
        var displayLabel = match.UserId is null
            ? null
            : await dbContext.Users
                .Where(user => user.Id == match.UserId)
                .Select(user => user.Name.FirstName)
                .SingleOrDefaultAsync(cancellationToken);

        return new CheckoutPassageLookupResult(match.Id, match.OccurredAt, lineCount, displayLabel);
    }

    private sealed record PassageCandidate(Guid Id, DateTime OccurredAt, UserId? UserId);
}
