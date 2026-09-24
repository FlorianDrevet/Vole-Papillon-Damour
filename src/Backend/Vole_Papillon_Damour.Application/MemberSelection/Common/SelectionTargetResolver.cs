using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.MemberSelection.Common;

public static class SelectionTargetResolver
{
    public static async Task<ErrorOr<SelectionTarget>> ResolveAsync(
        IProjectDbContext dbContext,
        string? isbn,
        Guid? rareBookId,
        CancellationToken cancellationToken)
    {
        var hasIsbn = !string.IsNullOrWhiteSpace(isbn);
        var hasRareBookId = rareBookId is { } id && id != Guid.Empty;
        if (hasIsbn == hasRareBookId)
        {
            return Errors.MemberSelection.InvalidTarget();
        }

        if (hasRareBookId)
        {
            var targetId = RareBookId.Create(rareBookId!.Value);
            var isPublished = await dbContext.RareBooks
                .AsNoTracking()
                .AnyAsync(
                    book => book.Id == targetId && book.Status == RareBookStatus.Published,
                    cancellationToken);
            return isPublished
                ? new SelectionTarget(null, targetId)
                : Errors.MemberSelection.NotInCatalog(rareBookId.Value.ToString());
        }

        if (!Isbn13.TryCreate(isbn, out var requestedIsbn))
        {
            return Errors.Book.InvalidIsbn(isbn!);
        }

        var book = await dbContext.Books
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == requestedIsbn, cancellationToken);
        var canonicalIsbn = book?.RedirectedToIsbn13 ?? requestedIsbn;
        if (canonicalIsbn != requestedIsbn)
        {
            book = await dbContext.Books
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == canonicalIsbn, cancellationToken);
        }

        if (book is not null)
        {
            return book.IsHiddenFromCatalog
                ? Errors.MemberSelection.NotInCatalog(canonicalIsbn.Value)
                : new SelectionTarget(canonicalIsbn, null);
        }

        var hasAnnouncement = await dbContext.BookAnnouncements
            .AsNoTracking()
            .AnyAsync(announcement => announcement.Isbn13 == canonicalIsbn, cancellationToken);
        return hasAnnouncement
            ? new SelectionTarget(canonicalIsbn, null)
            : Errors.MemberSelection.NotInCatalog(canonicalIsbn.Value);
    }
}
