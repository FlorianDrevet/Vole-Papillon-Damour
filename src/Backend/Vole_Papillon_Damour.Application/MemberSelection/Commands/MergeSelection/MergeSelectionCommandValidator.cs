using FluentValidation;

namespace Vole_Papillon_Damour.Application.MemberSelection.Commands.MergeSelection;

public sealed class MergeSelectionCommandValidator : AbstractValidator<MergeSelectionCommand>
{
    public MergeSelectionCommandValidator()
    {
        RuleFor(command => command.ExternalId).NotEmpty();
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(320);
        RuleFor(command => command.Entries)
            .NotNull()
            .Must(entries => entries.Count <= 200)
            .WithMessage("At most 200 selection entries can be merged at once.");
        RuleForEach(command => command.Entries)
            .Must(HasExactlyOneTarget)
            .WithMessage("Each selection entry must contain exactly one valid target.");
    }

    private static bool HasExactlyOneTarget(MergeSelectionEntry? entry)
    {
        if (entry is null)
        {
            return false;
        }

        var hasIsbn = !string.IsNullOrWhiteSpace(entry.Isbn13);
        var hasRareBookId = entry.RareBookId is { } rareBookId && rareBookId != Guid.Empty;
        return hasIsbn != hasRareBookId;
    }
}

