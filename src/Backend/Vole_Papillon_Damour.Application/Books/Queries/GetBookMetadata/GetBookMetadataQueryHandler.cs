using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetBookMetadata;

/// <summary>
/// Reads a book's catalog fiche before ever calling out to BnF, Open Library or
/// Google Books: a resolved or manually-edited book answers straight from the
/// database, and a book already marked <see cref="BookMetadataStatus.NotFound"/>
/// by the enrichment worker is not retried on every scan — the worker owns its
/// own backoff schedule for that. Only a book that is unknown or still
/// <see cref="BookMetadataStatus.Pending"/> reaches the (cached) resolver.
/// </summary>
public sealed class GetBookMetadataQueryHandler(
    IProjectDbContext dbContext,
    IBibliographicMetadataResolver resolver)
    : IRequestHandler<GetBookMetadataQuery, ErrorOr<BookMetadataResult>>
{
    public async Task<ErrorOr<BookMetadataResult>> Handle(
        GetBookMetadataQuery query,
        CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == query.Isbn13, cancellationToken);

        if (book is not null)
        {
            switch (book.MetadataStatus)
            {
                case BookMetadataStatus.Resolved or BookMetadataStatus.Manual:
                    return BuildResult(book);
                case BookMetadataStatus.NotFound:
                    return Errors.Book.MetadataNotFound(query.Isbn13.Value);
            }
        }

        var metadata = await resolver.ResolveAsync(query.Isbn13, cancellationToken);

        if (metadata is not null)
        {
            return metadata;
        }

        return Errors.Book.MetadataNotFound(query.Isbn13.Value);
    }

    private static BookMetadataResult BuildResult(Book book)
    {
        Uri? coverUrl = null;
        if (book.CoverUrl is not null)
        {
            Uri.TryCreate(book.CoverUrl, UriKind.Absolute, out coverUrl);
        }

        return new BookMetadataResult(
            book.Isbn13.Value,
            book.Title,
            book.Authors,
            book.Publisher,
            book.PublicationYear,
            coverUrl,
            MapSource(book.MetadataSource),
            book.WorkId,
            new DateTimeOffset(book.MetadataFetchedAt ?? book.UpdatedAt, TimeSpan.Zero),
            book.CoverSource is null ? null : MapCoverSource(book.CoverSource.Value),
            book.Genre);
    }

    private static string MapSource(BookMetadataSource? source) => source switch
    {
        BookMetadataSource.Bnf => "BnF",
        BookMetadataSource.OpenLibrary => "OpenLibrary",
        BookMetadataSource.GoogleBooks => "GoogleBooks",
        _ => "Manual",
    };

    private static string MapCoverSource(BookCoverSource source) => source switch
    {
        BookCoverSource.Bnf => "BnF",
        BookCoverSource.OpenLibrary => "OpenLibrary",
        BookCoverSource.GoogleBooks => "GoogleBooks",
        _ => "Manual",
    };
}
