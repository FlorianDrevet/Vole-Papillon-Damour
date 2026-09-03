using FluentValidation;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.UpdateBookMetadata;

public sealed class UpdateBookMetadataCommandValidator : AbstractValidator<UpdateBookMetadataCommand>
{
    public UpdateBookMetadataCommandValidator()
    {
        RuleFor(command => command.Isbn).NotEmpty();
        RuleFor(command => command.Fields)
            .NotNull()
            .Must(fields => fields is { Count: > 0 })
            .WithMessage("At least one metadata field must be selected.");
        RuleFor(command => command.Fields)
            .Must(fields => fields is not null && fields.All(Enum.IsDefined))
            .When(command => command.Fields is not null)
            .WithMessage("The metadata field is not supported.");
        RuleFor(command => command.UpdatedBy).NotNull();

        RuleFor(command => command.Title)
            .MaximumLength(500)
            .When(command => command.Fields?.Contains(BookMetadataField.Title) == true);
        RuleFor(command => command.Authors)
            .MaximumLength(500)
            .When(command => command.Fields?.Contains(BookMetadataField.Authors) == true);
        RuleFor(command => command.WorkId)
            .MaximumLength(64)
            .When(command => command.Fields?.Contains(BookMetadataField.WorkId) == true);
        RuleFor(command => command.Publisher)
            .MaximumLength(200)
            .When(command => command.Fields?.Contains(BookMetadataField.Publisher) == true);
        RuleFor(command => command.PhysicalFormat)
            .MaximumLength(50)
            .When(command => command.Fields?.Contains(BookMetadataField.PhysicalFormat) == true);
        RuleFor(command => command.Language)
            .MaximumLength(10)
            .When(command => command.Fields?.Contains(BookMetadataField.Language) == true);
        RuleFor(command => command.Genre)
            .MaximumLength(100)
            .When(command => command.Fields?.Contains(BookMetadataField.Genre) == true);
        RuleFor(command => command.CoverBlobRef)
            .MaximumLength(200)
            .When(command => command.Fields?.Contains(BookMetadataField.CoverBlobRef) == true);
        RuleFor(command => command.PublicationYear)
            .InclusiveBetween(1, 9999)
            .When(command => command.Fields?.Contains(BookMetadataField.PublicationYear) == true &&
                             command.PublicationYear.HasValue);
    }
}
