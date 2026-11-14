using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.RevokeFairDigestConsent;

public sealed class RevokeFairDigestConsentCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RevokeFairDigestConsentCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        RevokeFairDigestConsentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MemberId == Guid.Empty)
        {
            return Error.Validation("Recommendations.InvalidMember", "A member identifier is required.");
        }

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Recommendations.InvalidClock", "The recommendation clock must be expressed in UTC.");
        }

        var userId = UserId.Create(command.MemberId);
        var preference = await dbContext.MemberRecommendationPreferences
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
        if (preference is not null)
        {
            preference.RevokeFairDigestConsent(nowUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success;
    }
}
