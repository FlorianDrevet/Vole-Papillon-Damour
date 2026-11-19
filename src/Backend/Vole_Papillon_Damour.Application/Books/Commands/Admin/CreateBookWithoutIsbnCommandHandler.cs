using System.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class CreateBookWithoutIsbnCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreateBookWithoutIsbnCommand, ErrorOr<AdminBookOperationResult>>
{
    private const int MaximumQuantity = 100_000;

    public async Task<ErrorOr<AdminBookOperationResult>> Handle(
        CreateBookWithoutIsbnCommand command,
        CancellationToken cancellationToken)
    {
        var title = command.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 500)
        {
            return Error.Validation("Book.InvalidTitle", "A title of 500 characters or fewer is required.");
        }

        if (command.QuantityAvailable is < 1 or > MaximumQuantity)
        {
            return Errors.Book.InvalidCorrectionQuantity();
        }

        if (command.AddedBy is null || command.AddedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidAddedBy", "An adding user identifier is required.");
        }

        var addedAt = dateTimeProvider.UtcNow;
        if (addedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        var fields = BuildProvidedFields(command);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var sequence = await dbContext.InternalBookCodeSequences
            .SingleOrDefaultAsync(cancellationToken);
        if (sequence is null)
        {
            sequence = InternalBookCodeSequence.Create();
            dbContext.InternalBookCodeSequences.Add(sequence);
        }

        var code = sequence.IssueNext();
        var book = Book.CreateWithInternalCode(code, addedAt);
        try
        {
            book.ApplyManualMetadata(
                new BookMetadataPatch(
                    title,
                    Authors: Clean(command.Authors),
                    Publisher: Clean(command.Publisher),
                    PublicationYear: command.PublicationYear,
                    PhysicalFormat: Clean(command.PhysicalFormat),
                    Language: Clean(command.Language),
                    Genre: Clean(command.Genre),
                    CoverUrl: null,
                    fields,
                    Summary: Clean(command.Summary),
                    Pages: command.Pages,
                    Edition: Clean(command.Edition)),
                addedAt);
        }
        catch (ArgumentException)
        {
            return Errors.Book.InvalidMetadataValues();
        }

        dbContext.Books.Add(book);
        book.RecordAvailableEntry(addedAt, command.QuantityAvailable);
        var movement = BookMovement.Create(
            BookMovementId.CreateUnique(),
            code,
            BookMovementType.DirectEntry,
            command.QuantityAvailable,
            addedAt,
            addedAt,
            clockSuspect: false,
            scanSessionId: null,
            command.AddedBy,
            assoEventsId: null,
            "Création d'un livre sans ISBN",
            clientGestureId: null);
        dbContext.BookMovements.Add(movement);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Deux créations simultanées ont lu le même compteur : aucune des deux n'est enregistrée.
            return Error.Conflict(
                "Book.InternalCodeConflict",
                "La création a été concurrente d'une autre. Réessayez.");
        }

        await transaction.CommitAsync(cancellationToken);

        return new AdminBookOperationResult(
            code.Value,
            book.QuantityAvailable,
            QuantityAnnounced: 0,
            Changed: true,
            movement.Id.Value);
    }

    private static List<BookMetadataField> BuildProvidedFields(CreateBookWithoutIsbnCommand command)
    {
        var fields = new List<BookMetadataField> { BookMetadataField.Title };
        AddIfProvided(fields, BookMetadataField.Authors, command.Authors);
        AddIfProvided(fields, BookMetadataField.Publisher, command.Publisher);
        AddIfProvided(fields, BookMetadataField.PhysicalFormat, command.PhysicalFormat);
        AddIfProvided(fields, BookMetadataField.Language, command.Language);
        AddIfProvided(fields, BookMetadataField.Genre, command.Genre);
        AddIfProvided(fields, BookMetadataField.Summary, command.Summary);
        AddIfProvided(fields, BookMetadataField.Edition, command.Edition);
        if (command.PublicationYear is not null)
        {
            fields.Add(BookMetadataField.PublicationYear);
        }

        if (command.Pages is not null)
        {
            fields.Add(BookMetadataField.Pages);
        }

        return fields;
    }

    private static void AddIfProvided(List<BookMetadataField> fields, BookMetadataField field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(field);
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}