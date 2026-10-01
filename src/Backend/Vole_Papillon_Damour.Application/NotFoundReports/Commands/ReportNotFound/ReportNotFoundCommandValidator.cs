using FluentValidation;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Commands.ReportNotFound;

public sealed class ReportNotFoundCommandValidator : AbstractValidator<ReportNotFoundCommand>
{
    public ReportNotFoundCommandValidator()
    {
        RuleFor(command => command.Comment)
            .MaximumLength(BookNotFoundReport.CommentMaxLength);
    }
}
