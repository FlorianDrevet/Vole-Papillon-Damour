using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Commands.Admin.DismissNotFound;

public sealed class DismissNotFoundTargetCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<DismissNotFoundTargetCommandHandler> logger)
    : IRequestHandler<DismissNotFoundTargetCommand, ErrorOr<NotFoundClosureResult>>
{
    public async Task<ErrorOr<NotFoundClosureResult>> Handle(
        DismissNotFoundTargetCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Note) || command.Note.Trim().Length > 500)
        {
            return Errors.NotFoundReport.InvalidClosureNote();
        }

        if (command.By is null || command.By.Value == Guid.Empty)
        {
            return Errors.NotFoundReport.InvalidAuthor();
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Errors.NotFoundReport.InvalidClosureTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var loaded = await NotFoundReportClosureSupport.LoadAsync(dbContext, command.Target, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var target = loaded.Value;
        foreach (var report in target.Reports)
        {
            report.Dismiss(command.By, command.Note, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        NotFoundReportClosureSupport.LogClosed(
            logger, target, "Dismissed", target.Reports.Count, withdrawn: null);

        return new NotFoundClosureResult(
            target.Reports.Count,
            WithdrawnQuantity: null,
            target.Book?.QuantityAvailable,
            MovementId: null);
    }
}
