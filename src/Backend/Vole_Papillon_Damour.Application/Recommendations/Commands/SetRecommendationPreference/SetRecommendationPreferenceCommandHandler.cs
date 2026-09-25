using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.SetRecommendationPreference;

public sealed class SetRecommendationPreferenceCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SetRecommendationPreferenceCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        SetRecommendationPreferenceCommand command,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.ExternalId, out var externalId) ||
            externalId == Guid.Empty ||
            string.IsNullOrWhiteSpace(command.Email))
        {
            return Error.Validation("Recommendations.InvalidMember", "A valid member identity is required.");
        }

        var member = await memberIdentityService.EnsureAsync(
            externalId,
            command.Email,
            command.FirstName,
            command.LastName,
            cancellationToken);
        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Recommendations.InvalidClock",
                "The recommendation clock must be expressed in UTC.");
        }

        var preference = await dbContext.MemberRecommendationPreferences
            .SingleOrDefaultAsync(candidate => candidate.UserId == member.Id, cancellationToken);
        if (preference is null)
        {
            dbContext.MemberRecommendationPreferences.Add(
                MemberRecommendationPreference.Create(member.Id, command.Enabled, nowUtc));
        }
        else
        {
            preference.Set(command.Enabled, nowUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
