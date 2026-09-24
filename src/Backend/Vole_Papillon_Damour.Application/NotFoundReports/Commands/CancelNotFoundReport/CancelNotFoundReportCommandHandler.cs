using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Commands.CancelNotFoundReport;

public sealed class CancelNotFoundReportCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CancelNotFoundReportCommand, ErrorOr<Deleted>>
{
    public async Task<ErrorOr<Deleted>> Handle(
        CancelNotFoundReportCommand command,
        CancellationToken cancellationToken)
    {
        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId,
            command.Email,
            command.FirstName,
            command.LastName,
            cancellationToken);
        var report = await dbContext.BookNotFoundReports.SingleOrDefaultAsync(
            candidate => candidate.Id == command.ReportId && candidate.UserId == user.Id,
            cancellationToken);
        if (report is null)
        {
            return Errors.NotFoundReport.NotFound(command.ReportId);
        }

        if (!report.IsOpen)
        {
            return Errors.NotFoundReport.AlreadyClosed();
        }

        report.Cancel(dateTimeProvider.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }
}
