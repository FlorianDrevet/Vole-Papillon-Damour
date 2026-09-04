using System.Data;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Services;

public sealed class MemberIdentityService(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<User> EnsureAsync(
        Guid externalId,
        string email,
        CancellationToken cancellationToken)
    {
        if (externalId == Guid.Empty)
        {
            throw new ArgumentException("A valid external identity is required.", nameof(externalId));
        }

        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 320)
        {
            throw new ArgumentException("A valid email address is required.", nameof(email));
        }

        var seenAt = dateTimeProvider.UtcNow;
        if (seenAt.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("The member identity clock must be expressed in UTC.");
        }

        var externalIdValue = externalId.ToString();
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        User? user = null;

        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            user = await dbContext.Users.SingleOrDefaultAsync(
                candidate => candidate.ExternalId == externalIdValue,
                cancellationToken);

            if (user is null)
            {
                user = User.CreateFromExternalIdentity(
                    UserId.Create(externalId),
                    externalIdValue,
                    email,
                    seenAt);
                dbContext.Users.Add(user);
            }
            else
            {
                user.SynchronizeExternalIdentity(externalIdValue, email, seenAt);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return user ?? throw new InvalidOperationException("The member identity was not persisted.");
    }
}
