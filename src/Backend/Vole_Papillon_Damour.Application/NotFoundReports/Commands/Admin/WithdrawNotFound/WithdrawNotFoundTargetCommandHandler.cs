using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Commands.Admin.WithdrawNotFound;

public sealed class WithdrawNotFoundTargetCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<WithdrawNotFoundTargetCommandHandler> logger)
    : IRequestHandler<WithdrawNotFoundTargetCommand, ErrorOr<NotFoundClosureResult>>
{
    public async Task<ErrorOr<NotFoundClosureResult>> Handle(
        WithdrawNotFoundTargetCommand command,
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

        if (!Enum.IsDefined(command.Reason))
        {
            return Errors.NotFoundReport.InvalidWithdrawalReason();
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
        if (target.Isbn13 is { } isbn13 && target.Book is { } book)
        {
            if (command.QuantityFound < 0 || command.QuantityFound > book.QuantityAvailable)
            {
                return Errors.NotFoundReport.InvalidFoundQuantity();
            }

            var quantityToWithdraw = book.QuantityAvailable - command.QuantityFound;
            if (quantityToWithdraw == 0)
            {
                foreach (var report in target.Reports)
                {
                    report.MarkFound(command.By, command.Note, now);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                NotFoundReportClosureSupport.LogClosed(
                    logger, target, "Found", target.Reports.Count, withdrawn: null);

                return new NotFoundClosureResult(
                    target.Reports.Count,
                    WithdrawnQuantity: 0,
                    book.QuantityAvailable,
                    MovementId: null);
            }

            var withdrawal = await BookWithdrawal.WithdrawAsync(
                dbContext,
                isbn13.Value,
                quantityToWithdraw,
                command.Note,
                command.By,
                now,
                cancellationToken);
            if (withdrawal.IsError)
            {
                return withdrawal.Errors;
            }

            foreach (var report in target.Reports)
            {
                report.MarkWithdrawn(
                    command.By,
                    command.Reason,
                    withdrawal.Value.QuantityWithdrawn,
                    withdrawal.Value.MovementId,
                    command.Note,
                    now);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            NotFoundReportClosureSupport.LogClosed(
                logger,
                target,
                "Withdrawn",
                target.Reports.Count,
                withdrawal.Value.QuantityWithdrawn);

            return new NotFoundClosureResult(
                target.Reports.Count,
                withdrawal.Value.QuantityWithdrawn,
                withdrawal.Value.QuantityAvailable,
                withdrawal.Value.MovementId.Value);
        }

        if (target.RareBook is not { } rareBook)
        {
            return Errors.NotFoundReport.InvalidTarget();
        }

        rareBook.Unpublish(now, command.By);
        foreach (var report in target.Reports)
        {
            report.MarkWithdrawn(
                command.By,
                command.Reason,
                withdrawnQuantity: 0,
                movementId: null,
                command.Note,
                now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        NotFoundReportClosureSupport.LogClosed(
            logger, target, "Withdrawn", target.Reports.Count, withdrawn: null);

        return new NotFoundClosureResult(
            target.Reports.Count,
            WithdrawnQuantity: null,
            QuantityAvailable: null,
            MovementId: null);
    }
}
