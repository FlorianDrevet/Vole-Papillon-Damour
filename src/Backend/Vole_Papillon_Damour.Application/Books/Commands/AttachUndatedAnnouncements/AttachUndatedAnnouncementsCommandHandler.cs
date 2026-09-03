using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.AttachUndatedAnnouncements;

public sealed class AttachUndatedAnnouncementsCommandHandler(IProjectDbContext dbContext)
    : IRequestHandler<AttachUndatedAnnouncementsCommand, ErrorOr<AttachUndatedAnnouncementsResult>>
{
    public async Task<ErrorOr<AttachUndatedAnnouncementsResult>> Handle(
        AttachUndatedAnnouncementsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TargetAssoEventsId is null)
        {
            return Errors.Book.FairNotFound(Guid.Empty);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var targetFair = await dbContext.AssoEvents
            .SingleOrDefaultAsync(
                assoEvent => assoEvent.Id == command.TargetAssoEventsId,
                cancellationToken);
        if (targetFair is null)
        {
            return Errors.Book.FairNotFound(command.TargetAssoEventsId.Value);
        }

        if (targetFair.EventsType?.Value != EventsType.EventsTypeEnum.Books)
        {
            return Errors.Book.TargetFairMustBeBooks();
        }

        var announcements = await dbContext.BookAnnouncements
            .Where(announcement =>
                announcement.AssoEventsId == null &&
                announcement.Status == BookAnnouncementStatus.Announced)
            .ToListAsync(cancellationToken);

        var attachedCount = announcements.Count(announcement => announcement.AttachTo(targetFair.Id));
        if (attachedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new AttachUndatedAnnouncementsResult(targetFair.Id, attachedCount);
    }
}
