using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.Favorites.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.FavoriteBookAggregate;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.AddMyFavorite;

public sealed class AddMyFavoriteCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    FavoriteReferenceService favoriteReferenceService,
    IDateTimeProvider dateTimeProvider,
    IOptions<FavoritesOptions> options)
    : IRequestHandler<AddMyFavoriteCommand, ErrorOr<FavoriteAddedResult>>
{
    public async Task<ErrorOr<FavoriteAddedResult>> Handle(
        AddMyFavoriteCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn13, out var isbn13))
        {
            return Errors.Favorites.InvalidIsbn(command.Isbn13);
        }

        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId,
            command.Email,
            command.FirstName,
            command.LastName,
            cancellationToken);
        var existing = await dbContext.MemberFavoriteBooks.SingleOrDefaultAsync(
            favorite => favorite.UserId == user.Id && favorite.Isbn13 == isbn13,
            cancellationToken);
        var itemCount = await dbContext.MemberFavoriteBooks.CountAsync(
            favorite => favorite.UserId == user.Id,
            cancellationToken);
        if (existing is not null)
        {
            return new FavoriteAddedResult(existing.Isbn13, AlreadyPresent: true, itemCount);
        }

        if (itemCount >= options.Value.AccountMax)
        {
            return Errors.Favorites.LimitReached(options.Value.AccountMax);
        }

        var reference = await favoriteReferenceService.EnsureAsync(
            isbn13,
            FavoriteOrigin.Member,
            cancellationToken);
        if (reference.IsError)
        {
            return reference.Errors;
        }

        var canonicalIsbn13 = reference.Value.CanonicalIsbn13;
        existing = await dbContext.MemberFavoriteBooks.SingleOrDefaultAsync(
            favorite => favorite.UserId == user.Id && favorite.Isbn13 == canonicalIsbn13,
            cancellationToken);
        if (existing is not null)
        {
            return new FavoriteAddedResult(existing.Isbn13, AlreadyPresent: true, itemCount);
        }

        if (itemCount >= options.Value.AccountMax)
        {
            return Errors.Favorites.LimitReached(options.Value.AccountMax);
        }

        dbContext.MemberFavoriteBooks.Add(MemberFavoriteBook.Create(
            user.Id,
            canonicalIsbn13,
            dateTimeProvider.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new FavoriteAddedResult(canonicalIsbn13, AlreadyPresent: false, itemCount + 1);
    }
}
