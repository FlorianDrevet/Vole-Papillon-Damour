using System.Globalization;
using System.Text;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RareBookAggregate;

namespace Vole_Papillon_Damour.Application.Purchases.Queries.GetMyPurchases;

public sealed class GetMyPurchasesQueryHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService)
    : IRequestHandler<GetMyPurchasesQuery, ErrorOr<MyPurchasesPage>>
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public async Task<ErrorOr<MyPurchasesPage>> Handle(
        GetMyPurchasesQuery query,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(query.ExternalId, out var externalId) || externalId == Guid.Empty)
        {
            return Error.Validation("Purchases.InvalidMember", "A valid member identity is required.");
        }

        var member = await memberIdentityService.EnsureAsync(
            externalId,
            query.Email,
            query.FirstName,
            query.LastName,
            cancellationToken);
        if (!TryDecodeCursor(query.Cursor, out var cursor))
        {
            return Error.Validation("Purchases.InvalidCursor", "The purchase history cursor is invalid.");
        }

        var passagesQuery = dbContext.CheckoutPassages
            .AsNoTracking()
            .Where(passage =>
                passage.UserId == member.Id &&
                passage.Status == CheckoutPassageStatus.Associated &&
                dbContext.CheckoutPassageLines.Any(line => line.CheckoutPassageId == passage.Id));

        if (cursor is not null)
        {
            passagesQuery = passagesQuery.Where(passage =>
                passage.OccurredAt < cursor.OccurredAt ||
                (passage.OccurredAt == cursor.OccurredAt && passage.Id.CompareTo(cursor.Id) < 0));
        }

        var selectedPassages = await passagesQuery
            .OrderByDescending(passage => passage.OccurredAt)
            .ThenByDescending(passage => passage.Id)
            .Take(query.Limit + 1)
            .Select(passage => new PassagePointer(passage.Id, passage.OccurredAt))
            .ToListAsync(cancellationToken);

        var hasMore = selectedPassages.Count > query.Limit;
        if (hasMore)
        {
            selectedPassages.RemoveAt(selectedPassages.Count - 1);
        }

        if (selectedPassages.Count == 0)
        {
            return new MyPurchasesPage([], null);
        }

        var passageIds = selectedPassages.Select(passage => passage.Id).ToArray();
        var lines = await dbContext.CheckoutPassageLines
            .AsNoTracking()
            .Where(line => passageIds.Contains(line.CheckoutPassageId))
            .OrderBy(line => line.OccurredAt)
            .ThenBy(line => line.Id)
            .ToListAsync(cancellationToken);

        var editionIsbns = lines
            .Where(line => line.Isbn13 is not null && line.RareBookId is null)
            .Select(line => line.Isbn13!.Value)
            .ToHashSet();
        var rareBookIds = lines
            .Where(line => line.RareBookId is not null)
            .Select(line => line.RareBookId!)
            .ToHashSet();
        List<Book> books = editionIsbns.Count == 0
            ? []
            : await dbContext.Books.AsNoTracking()
                .Where(book => editionIsbns.Contains(book.Id))
                .ToListAsync(cancellationToken);
        List<RareBook> rareBooks = rareBookIds.Count == 0
            ? []
            : await dbContext.RareBooks.AsNoTracking()
                .Include(book => book.Photos)
                .Where(book => rareBookIds.Contains(book.Id))
                .ToListAsync(cancellationToken);

        var fairIds = lines
            .Where(line => line.AssoEventsId is not null)
            .Select(line => line.AssoEventsId!)
            .ToHashSet();
        List<AssoEvents> fairs = fairIds.Count == 0
            ? []
            : await dbContext.AssoEvents.AsNoTracking()
                .Where(fair => fairIds.Contains(fair.Id))
                .ToListAsync(cancellationToken);

        var linesByPassage = lines.GroupBy(line => line.CheckoutPassageId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var passages = selectedPassages.Select(passage =>
        {
            var passageLines = linesByPassage[passage.Id];
            var fairLine = passageLines.FirstOrDefault(line => line.AssoEventsId is not null);
            var fair = fairLine?.AssoEventsId is { } fairId
                ? fairs.SingleOrDefault(candidate => candidate.Id == fairId)
                : null;
            var results = passageLines.Select(line => ProjectLine(line, books, rareBooks)).ToArray();
            return new PurchasePassageResult(
                passage.Id,
                passage.Id.ToString("N")[..8].ToUpperInvariant(),
                new DateTimeOffset(DateTime.SpecifyKind(passage.OccurredAt, DateTimeKind.Utc)),
                fair?.Id.Value,
                fair is null ? null : $"Bourse aux livres · {fair.DateStart.ToString("d MMMM yyyy", FrenchCulture)}",
                passageLines.Where(line => line.VoidedAt is null).Sum(line => line.Quantity),
                results);
        }).ToArray();

        var nextCursor = hasMore && selectedPassages.Count > 0
            ? EncodeCursor(selectedPassages[^1].OccurredAt, selectedPassages[^1].Id)
            : null;
        return new MyPurchasesPage(passages, nextCursor);
    }

    private static PurchaseLineResult ProjectLine(
        CheckoutPassageLine line,
        IReadOnlyCollection<Book> books,
        IReadOnlyCollection<RareBook> rareBooks)
    {
        var currentCoverUrl = line.RareBookId is { } rareBookId
            ? rareBooks.SingleOrDefault(book => book.Id == rareBookId)?.Photos
                .OrderBy(photo => photo.Position)
                .FirstOrDefault()?.BlobUri.ToString()
            : line.Isbn13 is { } isbn13
                ? books.SingleOrDefault(book => book.Id == isbn13)?.CoverUrl
                : null;

        return new PurchaseLineResult(
            line.Id,
            line.RareBookId is null ? "edition" : "rare",
            line.Isbn13?.Value,
            line.RareBookId?.Value,
            line.Title,
            line.Authors,
            line.Publisher,
            line.PublicationYear,
            line.PhysicalFormat,
            line.Quantity,
            line.VoidedAt is null ? "Associated" : "Cancelled",
            currentCoverUrl);
    }

    private static string EncodeCursor(DateTime occurredAt, Guid id)
    {
        var value = $"{occurredAt.Ticks}:{id:D}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryDecodeCursor(string? encoded, out PurchaseCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return true;
        }

        try
        {
            var base64 = encoded.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
            var value = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var separator = value.IndexOf(':');
            if (separator <= 0 ||
                !long.TryParse(value[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParseExact(value[(separator + 1)..], "D", out var id) ||
                ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            {
                return false;
            }

            cursor = new PurchaseCursor(new DateTime(ticks, DateTimeKind.Utc), id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed record PassagePointer(Guid Id, DateTime OccurredAt);
    private sealed record PurchaseCursor(DateTime OccurredAt, Guid Id);
}
