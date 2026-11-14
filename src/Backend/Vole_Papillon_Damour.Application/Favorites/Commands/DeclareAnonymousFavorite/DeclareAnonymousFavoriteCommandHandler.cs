using System.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Favorites.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.FavoriteBookAggregate;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.DeclareAnonymousFavorite;

public sealed class DeclareAnonymousFavoriteCommandHandler(
    IProjectDbContext dbContext,
    FavoriteReferenceService favoriteReferenceService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<DeclareAnonymousFavoriteCommand, ErrorOr<AnonymousFavoriteResult>>
{
    public async Task<ErrorOr<AnonymousFavoriteResult>> Handle(
        DeclareAnonymousFavoriteCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn13, out var isbn13))
        {
            return Errors.Favorites.InvalidIsbn(command.Isbn13);
        }

        var reference = await favoriteReferenceService.EnsureAsync(
            isbn13,
            FavoriteOrigin.Anonymous,
            cancellationToken);
        if (reference.IsError)
        {
            return reference.Errors;
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Favorites.InvalidClock", "The favorites clock must be expressed in UTC.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var canonicalIsbn13 = reference.Value.CanonicalIsbn13;
        var count = await dbContext.AnonymousFavoriteCounts.SingleOrDefaultAsync(
            candidate => candidate.Id == canonicalIsbn13,
            cancellationToken);
        if (count is null)
        {
            count = AnonymousFavoriteCount.Create(canonicalIsbn13, now);
            dbContext.AnonymousFavoriteCounts.Add(count);
        }

        var previousCount = count.Count;
        count.Increment(now);
        if (count.Count != previousCount)
        {
            var book = await dbContext.Books.SingleOrDefaultAsync(
                candidate => candidate.Id == canonicalIsbn13,
                cancellationToken);
            book?.RecordFavoriteCountChanged(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AnonymousFavoriteResult(canonicalIsbn13.Value, count.Count);
    }
}
