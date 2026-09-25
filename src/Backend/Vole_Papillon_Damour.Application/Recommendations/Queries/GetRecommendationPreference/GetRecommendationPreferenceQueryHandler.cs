using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Services;

namespace Vole_Papillon_Damour.Application.Recommendations.Queries.GetRecommendationPreference;

public sealed class GetRecommendationPreferenceQueryHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService)
    : IRequestHandler<GetRecommendationPreferenceQuery, ErrorOr<RecommendationPreferenceResult>>
{
    public async Task<ErrorOr<RecommendationPreferenceResult>> Handle(
        GetRecommendationPreferenceQuery query,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(query.ExternalId, out var externalId) ||
            externalId == Guid.Empty ||
            string.IsNullOrWhiteSpace(query.Email))
        {
            return Error.Validation("Recommendations.InvalidMember", "A valid member identity is required.");
        }

        var member = await memberIdentityService.EnsureAsync(
            externalId,
            query.Email,
            query.FirstName,
            query.LastName,
            cancellationToken);
        var preference = await dbContext.MemberRecommendationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == member.Id, cancellationToken);

        return new RecommendationPreferenceResult(preference?.Enabled ?? true);
    }
}
