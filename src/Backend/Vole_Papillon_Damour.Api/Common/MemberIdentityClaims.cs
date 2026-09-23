using System.Security.Claims;
using Vole_Papillon_Damour.Api.Authentication;

namespace Vole_Papillon_Damour.Api.Common;

internal static class MemberIdentityClaims
{
    public static bool TryGetMemberIdentity(
        ClaimsPrincipal principal,
        out EntraMemberIdentityClaims identity)
    {
        if (EntraMemberIdentityClaimsReader.TryRead(principal, out var parsed) && parsed is not null)
        {
            identity = parsed;
            return true;
        }

        identity = null!;
        return false;
    }
}
