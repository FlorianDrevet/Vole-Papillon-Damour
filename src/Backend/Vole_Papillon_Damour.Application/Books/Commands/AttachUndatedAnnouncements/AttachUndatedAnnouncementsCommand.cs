using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.AttachUndatedAnnouncements;

public sealed record AttachUndatedAnnouncementsCommand(
    AssoEventsId TargetAssoEventsId) : IRequest<ErrorOr<AttachUndatedAnnouncementsResult>>;
