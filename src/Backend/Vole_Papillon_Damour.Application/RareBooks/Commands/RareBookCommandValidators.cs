using FluentValidation;
using Vole_Papillon_Damour.Application.RareBooks.Common;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.CreateRareBook
{
    public sealed class CreateRareBookCommandValidator : AbstractValidator<CreateRareBookCommand>
    {
        public CreateRareBookCommandValidator()
        {
            RuleFor(command => command.Title).NotEmpty().MaximumLength(300);
            RuleFor(command => command.Condition).NotEmpty();
            RuleFor(command => command.Price)
                .GreaterThanOrEqualTo(0m)
                .LessThanOrEqualTo(99_999_999.99m);
            RuleFor(command => command.UserId).NotNull();
        }
    }
}

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.UpdateRareBook
{
    public sealed class UpdateRareBookCommandValidator : AbstractValidator<UpdateRareBookCommand>
    {
        public UpdateRareBookCommandValidator()
        {
            RuleFor(command => command.Title).NotEmpty().MaximumLength(300);
            RuleFor(command => command.Condition).NotEmpty();
            RuleFor(command => command.Price)
                .GreaterThanOrEqualTo(0m)
                .LessThanOrEqualTo(99_999_999.99m);
            RuleFor(command => command.RareBookId).NotNull();
            RuleFor(command => command.RowVersion).NotNull();
            RuleFor(command => command.UserId).NotNull();
        }
    }
}

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.PublishRareBook
{
    public sealed class PublishRareBookCommandValidator : AbstractValidator<PublishRareBookCommand>
    {
        public PublishRareBookCommandValidator()
        {
            RuleFor(command => command.RareBookId).NotNull();
            RuleFor(command => command.UserId).NotNull();
        }
    }
}

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.UnpublishRareBook
{
    public sealed class UnpublishRareBookCommandValidator : AbstractValidator<UnpublishRareBookCommand>
    {
        public UnpublishRareBookCommandValidator()
        {
            RuleFor(command => command.RareBookId).NotNull();
            RuleFor(command => command.UserId).NotNull();
        }
    }
}

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.DeleteRareBook
{
    public sealed class DeleteRareBookCommandValidator : AbstractValidator<DeleteRareBookCommand>
    {
        public DeleteRareBookCommandValidator()
        {
            RuleFor(command => command.RareBookId).NotNull();
            RuleFor(command => command.UserId).NotNull();
        }
    }
}

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.RestoreRareBookAvailability
{
    public sealed class RestoreRareBookAvailabilityCommandValidator : AbstractValidator<RestoreRareBookAvailabilityCommand>
    {
        public RestoreRareBookAvailabilityCommandValidator()
        {
            RuleFor(command => command.RareBookId).NotNull();
            RuleFor(command => command.UserId).NotNull();
        }
    }
}

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.MarkRareBookSold
{
    public sealed class MarkRareBookSoldCommandValidator : AbstractValidator<MarkRareBookSoldCommand>
    {
        public MarkRareBookSoldCommandValidator()
        {
            RuleFor(command => command.RareBookId).NotNull();
            RuleFor(command => command.OccurredAt).NotEqual(default(DateTime));
            RuleFor(command => command.UserId).NotNull();
        }
    }
}

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.AddRareBookPhoto
{
    public sealed class AddRareBookPhotoCommandValidator : AbstractValidator<AddRareBookPhotoCommand>
    {
        public AddRareBookPhotoCommandValidator()
        {
            RuleFor(command => command.RareBookId).NotNull();
            RuleFor(command => command.Stream).NotNull();
            RuleFor(command => command.FileName).NotEmpty();
            RuleFor(command => command.ContentType).NotEmpty();
            RuleFor(command => command.SizeBytes)
                .GreaterThan(0)
                .LessThanOrEqualTo(RareBookCommandSupport.MaxPhotoSizeBytes);
            RuleFor(command => command.Caption).MaximumLength(80);
            RuleFor(command => command.UserId).NotNull();
        }
    }
}

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.ReorderRareBookPhotos
{
    public sealed class ReorderRareBookPhotosCommandValidator : AbstractValidator<ReorderRareBookPhotosCommand>
    {
        public ReorderRareBookPhotosCommandValidator()
        {
            RuleFor(command => command.RareBookId).NotNull();
            RuleFor(command => command.OrderedPhotoIds).NotNull();
            RuleFor(command => command.UserId).NotNull();
        }
    }
}

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.UpdateRareBookPhotoCaption
{
    public sealed class UpdateRareBookPhotoCaptionCommandValidator : AbstractValidator<UpdateRareBookPhotoCaptionCommand>
    {
        public UpdateRareBookPhotoCaptionCommandValidator()
        {
            RuleFor(command => command.RareBookPhotoId).NotNull();
            RuleFor(command => command.Caption).MaximumLength(80);
            RuleFor(command => command.UserId).NotNull();
        }
    }
}

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.DeleteRareBookPhoto
{
    public sealed class DeleteRareBookPhotoCommandValidator : AbstractValidator<DeleteRareBookPhotoCommand>
    {
        public DeleteRareBookPhotoCommandValidator()
        {
            RuleFor(command => command.RareBookPhotoId).NotNull();
            RuleFor(command => command.UserId).NotNull();
        }
    }
}
