using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IFairDigestEmailRenderer
{
    FairDigestEmailContent Render(FairDigestEmailMessage message, string unsubscribeToken);
}
