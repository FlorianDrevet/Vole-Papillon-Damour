using FluentValidation;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.AddRequest;

public sealed class AddManualBookRequestCommandValidator : AbstractValidator<AddManualBookRequestCommand>
{
    public AddManualBookRequestCommandValidator()
    {
        RuleFor(command => command.RequesterId).NotEmpty();
        RuleFor(command => command.Scope).IsInEnum();
        RuleFor(command => command.WorkId).MaximumLength(ManualBookRequest.WorkIdMaxLength);
        RuleFor(command => command.Title).MaximumLength(ManualBookRequest.TextMaxLength);
        RuleFor(command => command.Authors).MaximumLength(ManualBookRequest.TextMaxLength);
        RuleFor(command => command.Publisher).MaximumLength(ManualBookRequest.TextMaxLength);
        RuleFor(command => command.PublicationYear)
            .InclusiveBetween(1, 9999)
            .When(command => command.PublicationYear.HasValue);
    }
}
