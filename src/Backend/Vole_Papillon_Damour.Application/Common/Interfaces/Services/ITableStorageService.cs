namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface ITableStorageService
{
    Task<bool> AddMailToNewsletterAsync(string email, CancellationToken cancellationToken);
    Task<bool> DeleteMailFromNewsletterAsync(string email, CancellationToken cancellationToken);
    Task<bool> IsEmailInMailingListAsync(string email, CancellationToken cancellationToken);
    Task<List<string>> GetMailingListAsync(CancellationToken cancellationToken);
}