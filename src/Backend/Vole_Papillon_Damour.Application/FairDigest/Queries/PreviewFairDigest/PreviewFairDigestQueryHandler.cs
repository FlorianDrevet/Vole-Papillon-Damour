using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.FairDigest.Queries.PreviewFairDigest;

public sealed class PreviewFairDigestQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IOptions<FairDigestOptions> options,
    IFairDigestContentService contentService)
    : IRequestHandler<PreviewFairDigestQuery, ErrorOr<FairDigestPreviewResult>>
{
    public async Task<ErrorOr<FairDigestPreviewResult>> Handle(
        PreviewFairDigestQuery query,
        CancellationToken cancellationToken)
    {
        var admin = await dbContext.Users
            .SingleOrDefaultAsync(user => user.Id == query.AdministratorId, cancellationToken);
        if (admin is null)
        {
            return Error.Validation("FairDigest.AdministratorNotFound", "The administrator account was not found.");
        }

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("FairDigest.InvalidClock", "The fair digest clock must be expressed in UTC.");
        }

        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .WhereActiveBookFair()
            .OrderBy(assoEvent => assoEvent.DateStart)
            .ToListAsync(cancellationToken);
        var schedule = FairDigestSchedule.Calculate(
            fairs.Select(assoEvent => new FairDigestEvent(
                assoEvent.Id.Value,
                assoEvent.EventsType.Value,
                assoEvent.DateStart,
                assoEvent.DateEnd,
                assoEvent.IsCancelled,
                assoEvent.Name)),
            new DateTimeOffset(nowUtc, TimeSpan.Zero),
            options.Value.DaysBefore,
            options.Value.SendHourParis);
        if (schedule.NextFair is null)
        {
            return Error.Validation("FairDigest.NoUpcomingFair", "A future book fair is required to build the preview.");
        }

        var hasFavorites = await dbContext.MemberFavoriteBooks
            .AnyAsync(favorite => favorite.UserId == admin.Id, cancellationToken);
        var content = hasFavorites
            ? await contentService.BuildAsync(admin, schedule, cancellationToken)
            : FairDigestPreviewSamples.Create();
        if (content.IsError)
        {
            return content.Errors;
        }

        var campaign = await dbContext.FairDigestCampaigns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.AssoEventsId == AssoEventsId.Create(schedule.NextFair.Id),
                cancellationToken);
        return new FairDigestPreviewResult(
            campaign?.CampaignId ?? Guid.NewGuid(),
            schedule.NextFair,
            content.Value,
            IsExample: !hasFavorites);
    }
}

internal static class FairDigestPreviewSamples
{
    public static FairDigestMemberContent Create()
    {
        var book = new PublicCatalogBookResult(
            "9782070363735",
            "L'Étranger",
            "Albert Camus",
            "Gallimard",
            1942,
            "Paperback",
            "fr",
            "Roman",
            "OL123W",
            null,
            1,
            0,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            false);
        return new FairDigestMemberContent(
            [new PersonalRecommendation(book, NeighborReason.Theme, "Le Petit Prince", 4)],
            [],
            SkippedNoContent: false);
    }
}
