using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.UpdateBookMetadata;

public sealed class UpdateBookMetadataCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateBookMetadataCommand, ErrorOr<UpdateBookMetadataResult>>
{
    public async Task<ErrorOr<UpdateBookMetadataResult>> Handle(
        UpdateBookMetadataCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(command.Isbn);
        }

        if (command.Fields is null || command.Fields.Count == 0)
        {
            return Errors.Book.InvalidMetadataFields();
        }

        var fields = command.Fields.Distinct().ToArray();
        if (fields.Any(field => !Enum.IsDefined(field)) || !HasValidValues(command, fields))
        {
            return Errors.Book.InvalidMetadataValues();
        }

        if (command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        var updatedAt = dateTimeProvider.UtcNow;
        if (updatedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        if (book?.RedirectedToIsbn13 is { } canonicalIsbn13)
        {
            isbn13 = canonicalIsbn13;
            book = await dbContext.Books
                .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        }

        if (book is null)
        {
            return Errors.Book.NotFound(isbn13.Value);
        }

        var changed = book.ApplyManualMetadata(
            new BookMetadataPatch(
                command.Title,
                command.Authors,
                command.Publisher,
                command.PublicationYear,
                command.PhysicalFormat,
                command.Language,
                command.Genre,
                command.CoverBlobRef,
                fields),
            updatedAt);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new UpdateBookMetadataResult(
            book.Isbn13.Value,
            book.MetadataStatus,
            book.MetadataSource!.Value,
            book.ManuallyEditedFields,
            book.UpdatedAt,
            changed);
    }

    private static bool HasValidValues(
        UpdateBookMetadataCommand command,
        IEnumerable<BookMetadataField> fields)
    {
        foreach (var field in fields)
        {
            var isValid = field switch
            {
                BookMetadataField.Title => IsWithinLength(command.Title, 500),
                BookMetadataField.Authors => IsWithinLength(command.Authors, 500),
                BookMetadataField.Publisher => IsWithinLength(command.Publisher, 200),
                BookMetadataField.PublicationYear => command.PublicationYear is null or (>= 1 and <= 9999),
                BookMetadataField.PhysicalFormat => IsWithinLength(command.PhysicalFormat, 50),
                BookMetadataField.Language => IsWithinLength(command.Language, 10),
                BookMetadataField.Genre => IsWithinLength(command.Genre, 100),
                BookMetadataField.CoverBlobRef => IsWithinLength(command.CoverBlobRef, 200),
                _ => false
            };

            if (!isValid)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWithinLength(string? value, int maxLength)
    {
        return value is null || value.Length <= maxLength;
    }
}
