using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Azure;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.TableStorageService;

public class TableStorageService: ITableStorageService
{
    private readonly TableServiceClient _tableServiceClient;
    
    public TableStorageService(IAzureClientFactory<TableServiceClient> azureClientFactory)
    {
        _tableServiceClient = azureClientFactory.CreateClient("MailingListStorage");
    }
    
    public async Task<bool> AddMailToNewsletterAsync(string email, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient("mailinglist");
        var tableEntity = new TableEntity("Newsletter", email);
        await tableClient.AddEntityAsync(tableEntity, cancellationToken: cancellationToken);
        return true;
    }

    public async Task<bool> DeleteMailFromNewsletterAsync(string email, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient("mailinglist");
        var response = await tableClient.DeleteEntityAsync("Newsletter", email, cancellationToken: cancellationToken);
        return response.Status == 204;
    }

    public Task<bool> IsEmailInMailingListAsync(string email, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient("mailinglist");
        return tableClient.GetEntityAsync<TableEntity>("Newsletter", email, cancellationToken: cancellationToken)
            .ContinueWith(task => task.IsCompletedSuccessfully, cancellationToken);
    }

    public async Task<List<string>> GetMailingListAsync(CancellationToken cancellationToken)
    {
        var result = new List<string>();
        var tableClient = _tableServiceClient.GetTableClient("mailinglist");
        
        var query = tableClient.QueryAsync<TableEntity>(cancellationToken: cancellationToken);
        await foreach (var page in query.AsPages().WithCancellation(cancellationToken))
        {
            result.AddRange(page.Values.Select(qEntity => qEntity.RowKey));
        }
        return result;
    }
}