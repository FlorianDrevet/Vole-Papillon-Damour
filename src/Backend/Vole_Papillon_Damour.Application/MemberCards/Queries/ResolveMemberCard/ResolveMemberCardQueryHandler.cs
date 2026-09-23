using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.MemberCardAggregate;

namespace Vole_Papillon_Damour.Application.MemberCards.Queries.ResolveMemberCard;

public sealed class ResolveMemberCardQueryHandler(
    IProjectDbContext dbContext,
    IMemberCardTokenService tokens,
    ILogger<ResolveMemberCardQueryHandler>? logger = null)
    : IRequestHandler<ResolveMemberCardQuery, ErrorOr<MemberCardConfirmation>>
{
    public async Task<ErrorOr<MemberCardConfirmation>> Handle(
        ResolveMemberCardQuery request,
        CancellationToken cancellationToken)
    {
        var credentialType = GetCredentialType(request.Credential, tokens);
        var resolved = await MemberCardResolver.ResolveAsync(dbContext, tokens, request.Credential, cancellationToken);
        if (resolved.IsError)
        {
            logger?.LogInformation("MemberCardNotRecognised {CredentialType}", credentialType);
            return resolved.Errors;
        }

        logger?.LogInformation("MemberCardResolved {CredentialType}", credentialType);
        return new MemberCardConfirmation(resolved.Value.DisplayLabel);
    }

    private static string GetCredentialType(string credential, IMemberCardTokenService tokens)
    {
        if (tokens.TryRead(credential, out _, out _))
        {
            return "qr";
        }

        return MemberCardRecoveryCode.TryNormalize(credential, out _)
            ? "recovery-code"
            : "other";
    }
}
