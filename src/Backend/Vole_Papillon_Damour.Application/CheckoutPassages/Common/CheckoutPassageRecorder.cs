using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.CheckoutPassages.Common;

public sealed class CheckoutPassageRecorder(
    IProjectDbContext dbContext,
    ILogger<CheckoutPassageRecorder> logger)
{
    public async Task RecordOrdinarySaleAsync(
        Guid passageId,
        BookMovement movement,
        Book book,
        string requestedIsbn,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(book);

        var alreadyRecorded = dbContext.CheckoutPassageLines.Local
            .Any(line => line.SaleMovementId == movement.Id)
            || await dbContext.CheckoutPassageLines
                .AnyAsync(line => line.SaleMovementId == movement.Id, cancellationToken);

        if (!alreadyRecorded)
        {
            var passage = await GetOrOpenAsync(passageId, movement.OccurredAt, cancellationToken);
            var line = CheckoutPassageLine.ForOrdinarySale(
                Guid.NewGuid(), passageId, movement, book, requestedIsbn);
            dbContext.CheckoutPassageLines.Add(line);
            await MarkSelectionPurchasedAsync(passage, cancellationToken);
        }

        logger.LogInformation(
            "CheckoutPassageLineRecorded {PassageId} {Kind} {AlreadyRecorded}",
            passageId,
            "ordinary",
            alreadyRecorded);
    }

    public async Task RecordRareSaleAsync(
        Guid passageId,
        RareBook rareBook,
        DateTime occurredAt,
        AssoEventsId? fairId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rareBook);

        var alreadyRecorded = dbContext.CheckoutPassageLines.Local
            .Any(line => line.CheckoutPassageId == passageId && line.RareBookId == rareBook.Id)
            || await dbContext.CheckoutPassageLines.AnyAsync(
                line => line.CheckoutPassageId == passageId && line.RareBookId == rareBook.Id,
                cancellationToken);

        if (!alreadyRecorded)
        {
            var passage = await GetOrOpenAsync(passageId, occurredAt, cancellationToken);
            dbContext.CheckoutPassageLines.Add(CheckoutPassageLine.ForRareSale(
                Guid.NewGuid(), passageId, rareBook, occurredAt, fairId));
            await MarkSelectionPurchasedAsync(passage, cancellationToken);
        }

        logger.LogInformation(
            "CheckoutPassageLineRecorded {PassageId} {Kind} {AlreadyRecorded}",
            passageId,
            "rare",
            alreadyRecorded);
    }

    public async Task<CheckoutPassage> GetOrOpenAsync(
        Guid passageId,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        var localPassage = dbContext.CheckoutPassages.Local
            .SingleOrDefault(passage => passage.Id == passageId);
        if (localPassage is not null)
        {
            return localPassage;
        }

        var existingPassage = await dbContext.CheckoutPassages
            .SingleOrDefaultAsync(passage => passage.Id == passageId, cancellationToken);
        if (existingPassage is not null)
        {
            return existingPassage;
        }

        var passage = CheckoutPassage.OpenFromSale(passageId, occurredAt, occurredAt);
        dbContext.CheckoutPassages.Add(passage);
        return passage;
    }

    public async Task MarkSelectionPurchasedAsync(
        CheckoutPassage passage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(passage);
        if (!passage.IsVisibleToMember || passage.UserId is null)
        {
            logger.LogInformation(
                "SelectionMarkedPurchased {PassageId} {Count}", passage.Id, 0);
            return;
        }

        var lines = await dbContext.CheckoutPassageLines
            .Where(line => line.CheckoutPassageId == passage.Id && line.VoidedAt == null)
            .ToListAsync(cancellationToken);
        var lineIds = lines.Select(line => line.Id).ToHashSet();
        lines.AddRange(dbContext.CheckoutPassageLines.Local
            .Where(line => line.CheckoutPassageId == passage.Id
                           && line.VoidedAt == null
                           && lineIds.Add(line.Id)));

        if (lines.Count == 0)
        {
            logger.LogInformation(
                "SelectionMarkedPurchased {PassageId} {Count}", passage.Id, 0);
            return;
        }

        var isbns = lines
            .SelectMany(line => new[] { line.Isbn13, ParseRequestedIsbn(line.RequestedIsbn13) })
            .Where(isbn => isbn is not null)
            .Select(isbn => isbn!)
            .Distinct()
            .ToArray();
        var rareBookIds = lines
            .Where(line => line.RareBookId is not null)
            .Select(line => line.RareBookId!)
            .Distinct()
            .ToArray();

        if (isbns.Length == 0 && rareBookIds.Length == 0)
        {
            logger.LogInformation(
                "SelectionMarkedPurchased {PassageId} {Count}", passage.Id, 0);
            return;
        }

        var selectionItems = await dbContext.MemberSelectionItems
            .Where(item => item.UserId == passage.UserId
                           && ((item.Isbn13 != null && isbns.Contains(item.Isbn13))
                               || (item.RareBookId != null && rareBookIds.Contains(item.RareBookId))))
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var item in selectionItems)
        {
            var matchingLine = lines
                .Where(line => Matches(item, line))
                .OrderBy(line => line.OccurredAt)
                .FirstOrDefault();
            if (matchingLine is not null && item.MarkPurchased(matchingLine.OccurredAt))
            {
                count++;
            }
        }

        logger.LogInformation(
            "SelectionMarkedPurchased {PassageId} {Count}", passage.Id, count);
    }

    private static bool Matches(MemberSelectionItem item, CheckoutPassageLine line) =>
        (item.Isbn13 is not null
         && (item.Isbn13 == line.Isbn13
             || item.Isbn13.Value.Value == line.RequestedIsbn13))
        || (item.RareBookId is not null && item.RareBookId == line.RareBookId);

    private static Isbn13? ParseRequestedIsbn(string? value) =>
        Isbn13.TryCreate(value, out var isbn) ? isbn : null;
}
