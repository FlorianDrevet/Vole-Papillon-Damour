using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IFairDigestEmailSender
{
    bool IsEnabled { get; }

    Task SendAsync(FairDigestEmailMessage message, CancellationToken cancellationToken);
}
