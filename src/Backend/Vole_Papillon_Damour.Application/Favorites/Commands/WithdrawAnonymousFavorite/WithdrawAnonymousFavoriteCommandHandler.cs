using System.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Favorites.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.WithdrawAnonymousFavorite;

public sealed class WithdrawAnonymousFavoriteCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<WithdrawAnonymousFavoriteCommand, ErrorOr<AnonymousFavoriteResult>>
{
    public async Task<ErrorOr<AnonymousFavoriteResult>> Handle(
        WithdrawAnonymousFavoriteCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn13, out var isbn13))
        {
            return Errors.Favorites.InvalidIsbn(command.Isbn13);
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Favorites.InvalidClock", "The favorites clock must be expressed in UTC.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var book = await dbContext.Books.SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        var canonicalIsbn13 = book?.RedirectedToIsbn13 ?? isbn13;
        if (book?.RedirectedToIsbn13 is { } redirectedIsbn)
        {
            book = await dbContext.Books.SingleOrDefaultAsync(
                candidate => candidate.Id == redirectedIsbn,
                cancellationToken);
        }

        var count = await dbContext.AnonymousFavoriteCounts.SingleOrDefaultAsync(
            candidate => candidate.Id == canonicalIsbn13,
            cancellationToken);
        if (count is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new AnonymousFavoriteResult(canonicalIsbn13.Value, 0);
        }

        var previousCount = count.Count;
        count.Decrement(now);
        if (count.Count != previousCount)
        {
            book?.RecordFavoriteCountChanged(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AnonymousFavoriteResult(canonicalIsbn13.Value, count.Count);
    }
}
