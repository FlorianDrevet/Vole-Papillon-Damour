using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IBookAlertEmailSender
{
    bool IsEnabled { get; }

    Task SendAsync(
        BookAlertDelivery delivery,
        CancellationToken cancellationToken);
}
