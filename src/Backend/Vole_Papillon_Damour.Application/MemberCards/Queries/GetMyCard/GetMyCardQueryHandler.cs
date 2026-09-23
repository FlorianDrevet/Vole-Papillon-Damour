using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.MemberCards.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.MemberCards.Queries.GetMyCard;

public sealed class GetMyCardQueryHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider,
    MemberCardIssuer cardIssuer,
    IMemberCardTokenService tokens) : IRequestHandler<GetMyCardQuery, ErrorOr<MyCardResult>>
{
    public async Task<ErrorOr<MyCardResult>> Handle(GetMyCardQuery request, CancellationToken cancellationToken)
    {
        var user = await memberIdentityService.EnsureAsync(
            request.ExternalId,
            request.Email,
            request.FirstName,
            request.LastName,
            cancellationToken);
        var card = await cardIssuer.GetOrIssueAsync(dbContext, user.Id, dateTimeProvider.UtcNow, cancellationToken);
        if (!card.IsActive)
        {
            return Errors.MemberCard.NotRecognised();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResult(card, user.Name?.FirstName, tokens);
    }

    internal static MyCardResult ToResult(
        Domain.MemberCardAggregate.MemberCard card,
        string? firstName,
        IMemberCardTokenService tokens) =>
        new(
            tokens.Create(card.Id, card.Version),
            card.RecoveryCode,
            string.IsNullOrWhiteSpace(firstName) ? "Membre" : firstName.Trim(),
            new DateTimeOffset(card.IssuedAt, TimeSpan.Zero));
}
