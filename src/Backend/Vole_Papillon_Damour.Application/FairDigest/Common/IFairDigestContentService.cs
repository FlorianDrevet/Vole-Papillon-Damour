using ErrorOr;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Domain.UserAggregate;

namespace Vole_Papillon_Damour.Application.FairDigest.Common;

public interface IFairDigestContentService
{
    Task<ErrorOr<FairDigestMemberContent>> BuildAsync(
        User member,
        FairDigestScheduleResult schedule,
        CancellationToken cancellationToken);
}
