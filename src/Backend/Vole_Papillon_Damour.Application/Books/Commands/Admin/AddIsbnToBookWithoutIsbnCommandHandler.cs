using System.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

// RG-174 : un livre ordinaire à code interne reçoit un ISBN. Le code interne reste valide (étiquettes collées) :
// les deux codes résolvent vers la même fiche, une fiche ISBN, qui porte le stock. Si la fiche ISBN existe, la
// fiche interne y est fusionnée. Sinon elle est créée avec les métadonnées saisies à la main, marquées comme
// éditées pour que l'enrichissement ne les écrase pas, puis la fusion est appliquée.
public sealed class AddIsbnToBookWithoutIsbnCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    INotFoundReportLapser notFoundReportLapser)
    : IRequestHandler<AddIsbnToBookWithoutIsbnCommand, ErrorOr<AdminBookOperationResult>>
{
    private const string MergeNote = "Ajout d'un ISBN à une fiche sans ISBN (RG-174)";

    public async Task<ErrorOr<AdminBookOperationResult>> Handle(
        AddIsbnToBookWithoutIsbnCommand command,
        CancellationToken cancellationToken)
    {
        var internalCodeValue = command.InternalCode?.Trim() ?? string.Empty;
        if (!BookCode.IsValidInternalCode(internalCodeValue))
        {
            return Error.Validation(
                "Book.NotAnInternalCode",
                "Le livre à compléter doit porter un code interne.");
        }

        if (!Isbn13.TryCreate(command.Isbn13, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(command.Isbn13);
        }

        if (command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        var addedAt = dateTimeProvider.UtcNow;
        if (addedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var sourceCode = BookCode.CreateInternal(internalCodeValue);
        var source = await dbContext.Books.SingleOrDefaultAsync(
            candidate => candidate.Id == sourceCode,
            cancellationToken);
        if (source is null)
        {
            return Errors.Book.NotFound(internalCodeValue);
        }

        BookCode targetCode = isbn13;
        var target = await dbContext.Books.SingleOrDefaultAsync(
            candidate => candidate.Id == targetCode,
            cancellationToken);
        if (target is null)
        {
            var created = CreateIsbnFiche(source, targetCode, addedAt);
            if (created.IsError)
            {
                return created.Errors;
            }

            target = created.Value;
            dbContext.Books.Add(target);
        }

        var merged = await BookMergeOperation.ApplyAsync(
            dbContext,
            notFoundReportLapser,
            source,
            target,
            MergeNote,
            command.UpdatedBy,
            addedAt,
            cancellationToken);
        if (merged.IsError)
        {
            return merged.Errors;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // La fiche ISBN a été créée par une autre action entre-temps (clé primaire) : l'ajout se réessaie.
            return Error.Conflict(
                "Book.IsbnConflict",
                "L'ISBN vient d'être ajouté par une autre action. Réessayez.");
        }

        await transaction.CommitAsync(cancellationToken);

        var announcedQuantity = await dbContext.BookAnnouncements
            .Where(announcement =>
                announcement.Isbn13 == targetCode &&
                announcement.Status == BookAnnouncementStatus.Announced)
            .Select(announcement => (int?)announcement.Quantity)
            .SumAsync(cancellationToken) ?? 0;
        return new AdminBookOperationResult(
            isbn13.Value,
            target.QuantityAvailable,
            announcedQuantity,
            Changed: true);
    }

    // Fiche ISBN à créer : métadonnées de la fiche interne, toutes marquées comme éditées à la main.
    // La couverture n'est pas recopiée : une photo interne marquée « éditée » bloquerait la couverture fournisseur.
    private static ErrorOr<Book> CreateIsbnFiche(Book source, BookCode code, DateTime addedAt)
    {
        if (string.IsNullOrWhiteSpace(source.Title))
        {
            return Error.Validation(
                "Book.TitleRequired",
                "La fiche sans ISBN doit avoir un titre avant l'ajout d'un ISBN.");
        }

        var genreNames = source.Genres.Select(genre => genre.Name).ToArray();
        var fields = new List<BookMetadataField> { BookMetadataField.Title };
        AddIfPresent(fields, BookMetadataField.Authors, source.Authors);
        AddIfPresent(fields, BookMetadataField.Publisher, source.Publisher);
        AddIfPresent(fields, BookMetadataField.PhysicalFormat, source.PhysicalFormat);
        AddIfPresent(fields, BookMetadataField.Language, source.Language);
        AddIfPresent(fields, BookMetadataField.Summary, source.Summary);
        AddIfPresent(fields, BookMetadataField.Edition, source.Edition);
        if (genreNames.Length > 0)
        {
            fields.Add(BookMetadataField.Genre);
        }

        if (source.PublicationYear is not null)
        {
            fields.Add(BookMetadataField.PublicationYear);
        }

        if (source.Pages is not null)
        {
            fields.Add(BookMetadataField.Pages);
        }

        var patch = new BookMetadataPatch(
            Title: source.Title,
            Authors: source.Authors,
            Publisher: source.Publisher,
            PublicationYear: source.PublicationYear,
            PhysicalFormat: source.PhysicalFormat,
            Language: source.Language,
            Genre: source.Genre,
            CoverUrl: null,
            Fields: fields,
            Genres: genreNames,
            Summary: source.Summary,
            Pages: source.Pages,
            Edition: source.Edition);

        var book = Book.Create(code, addedAt);
        try
        {
            book.ApplyManualMetadata(patch, addedAt);
        }
        catch (ArgumentException)
        {
            return Errors.Book.InvalidMetadataValues();
        }

        return book;
    }

    private static void AddIfPresent(List<BookMetadataField> fields, BookMetadataField field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(field);
        }
    }
}
