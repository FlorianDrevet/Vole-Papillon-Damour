using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

namespace Vole_Papillon_Damour.Infrastructure.Persistence;

// Lit le numéro suivant de la séquence SQL. SQL Server interdit NEXT VALUE FOR dans une table dérivée :
// la requête est donc une commande ADO.NET de premier niveau, exécutée dans la transaction courante.
// Le numéro lu n'est pas annulé si la transaction échoue (il est sauté, RG-171).
public sealed class SqlServerInternalBookCodeIssuer(ProjectDbContext dbContext) : IInternalBookCodeIssuer
{
    private static readonly string NextValueSql =
        $"SELECT NEXT VALUE FOR [dbo].[{InternalBookCodeNumberingConfiguration.SequenceName}]";

    public async Task<BookCode> IssueNextAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = NextValueSql;
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

            var value = await command.ExecuteScalarAsync(cancellationToken);
            return BookCode.CreateInternalFromSequence(Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}
