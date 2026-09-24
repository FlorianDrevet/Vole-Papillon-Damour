using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.MemberSelection.Common;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Commands.ReportNotFound;

public sealed class ReportNotFoundCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReportNotFoundCommandHandler> logger)
    : IRequestHandler<ReportNotFoundCommand, ErrorOr<NotFoundReportCreatedResult>>
{
    public async Task<ErrorOr<NotFoundReportCreatedResult>> Handle(
        ReportNotFoundCommand command,
        CancellationToken cancellationToken)
    {
        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId,
            command.Email,
            command.FirstName,
            command.LastName,
            cancellationToken);

        var selectionItem = await dbContext.MemberSelectionItems
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == command.SelectionItemId && item.UserId == user.Id,
                cancellationToken);
        if (selectionItem is null)
        {
            return Errors.MemberSelection.NotFound(command.SelectionItemId);
        }

        if (selectionItem.Status == MemberSelectionStatus.Purchased)
        {
            return Errors.NotFoundReport.NotReportable();
        }

        var targetResult = await ResolveTargetAsync(selectionItem, cancellationToken);
        if (targetResult.IsError)
        {
            return targetResult.Errors;
        }

        var target = targetResult.Value;
        var availability = target.Isbn13 is { } isbn13
            ? await GetEditionAvailabilityAsync(isbn13, cancellationToken)
            : await GetRareBookAvailabilityAsync(target.RareBookId!, cancellationToken);
        if (availability != SelectionAvailability.Available)
        {
            return Errors.NotFoundReport.NotReportable();
        }

        var existing = await FindOpenReportAsync(user.Id, target, cancellationToken);
        if (existing is not null)
        {
            return ToResult(existing, alreadyOpen: true);
        }

        var now = dateTimeProvider.UtcNow;
        var dailyLimit = await dbContext.AssociationSettings
            .AsNoTracking()
            .Select(settings => (int?)settings.NotFoundReportDailyLimit)
            .SingleOrDefaultAsync(cancellationToken)
            ?? AssociationSettings.DefaultNotFoundReportDailyLimit;
        var reportsInLast24Hours = await dbContext.BookNotFoundReports
            .CountAsync(
                report => report.UserId == user.Id && report.ReportedAt > now.AddHours(-24),
                cancellationToken);
        if (reportsInLast24Hours >= dailyLimit)
        {
            return Errors.NotFoundReport.DailyLimitReached(dailyLimit);
        }

        var report = target.Isbn13 is { } targetIsbn
            ? BookNotFoundReport.CreateForEdition(
                Guid.NewGuid(), user.Id, targetIsbn, command.Location, command.Comment, now)
            : BookNotFoundReport.CreateForRareBook(
                Guid.NewGuid(), user.Id, target.RareBookId!, command.Location, command.Comment, now);
        dbContext.BookNotFoundReports.Add(report);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var racedReport = await FindOpenReportAsync(user.Id, target, cancellationToken);
            if (racedReport is null)
            {
                throw;
            }

            dbContext.BookNotFoundReports.Remove(report);
            return ToResult(racedReport, alreadyOpen: true);
        }

        var targetKind = target.Isbn13 is null ? "RareBook" : "Edition";
        logger.LogInformation(
            "NotFoundReportCreated {reportId} {targetKind} {alreadyOpen}",
            report.Id,
            targetKind,
            false);

        return ToResult(report, alreadyOpen: false);
    }

    private async Task<ErrorOr<NotFoundReportTarget>> ResolveTargetAsync(
        MemberSelectionItem selectionItem,
        CancellationToken cancellationToken)
    {
        if (selectionItem.Isbn13 is { } selectedIsbn)
        {
            var selectedBook = await dbContext.Books
                .AsNoTracking()
                .SingleOrDefaultAsync(book => book.Id == selectedIsbn, cancellationToken);
            var canonicalIsbn = selectedBook?.RedirectedToIsbn13 ?? selectedIsbn;
            return new NotFoundReportTarget(canonicalIsbn, null);
        }

        return selectionItem.RareBookId is { } rareBookId
            ? new NotFoundReportTarget(null, rareBookId)
            : Errors.NotFoundReport.NotReportable();
    }

    private async Task<SelectionAvailability> GetEditionAvailabilityAsync(
        Domain.BookAggregate.ValueObjects.Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        var announced = await dbContext.BookAnnouncements
            .AsNoTracking()
            .AnyAsync(announcement => announcement.Isbn13 == isbn13, cancellationToken);
        return SelectionAvailabilityProjector.ForEdition(book, announced);
    }

    private async Task<SelectionAvailability> GetRareBookAvailabilityAsync(
        Domain.RareBookAggregate.ValueObjects.RareBookId rareBookId,
        CancellationToken cancellationToken)
    {
        var rareBook = await dbContext.RareBooks
            .AsNoTracking()
            .SingleOrDefaultAsync(book => book.Id == rareBookId, cancellationToken);
        return SelectionAvailabilityProjector.ForRareBook(rareBook);
    }

    private Task<BookNotFoundReport?> FindOpenReportAsync(
        Domain.UserAggregate.ValueObjects.UserId userId,
        NotFoundReportTarget target,
        CancellationToken cancellationToken)
    {
        var query = dbContext.BookNotFoundReports
            .AsNoTracking()
            .Where(report => report.UserId == userId && report.Status == NotFoundReportStatus.Open);
        return target.Isbn13 is { } isbn13
            ? query.SingleOrDefaultAsync(report => report.Isbn13 == isbn13, cancellationToken)
            : query.SingleOrDefaultAsync(report => report.RareBookId == target.RareBookId, cancellationToken);
    }

    private static NotFoundReportCreatedResult ToResult(BookNotFoundReport report, bool alreadyOpen) =>
        new(report.Id, new DateTimeOffset(report.ReportedAt), alreadyOpen);

    private sealed record NotFoundReportTarget(
        Domain.BookAggregate.ValueObjects.Isbn13? Isbn13,
        Domain.RareBookAggregate.ValueObjects.RareBookId? RareBookId);
}
