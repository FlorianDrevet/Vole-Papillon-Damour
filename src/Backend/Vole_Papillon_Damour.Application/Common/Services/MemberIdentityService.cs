using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Services;

public sealed class MemberIdentityService(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IEntraUserDirectory? directory = null,
    ILogger<MemberIdentityService>? logger = null)
{
    public async Task<User> EnsureAsync(
        Guid externalId,
        string email,
        CancellationToken cancellationToken)
    {
        return await EnsureAsync(externalId, email, null, null, cancellationToken);
    }

    public async Task<User> EnsureAsync(
        Guid externalId,
        string email,
        string? displayName,
        CancellationToken cancellationToken)
    {
        return await EnsureCoreAsync(
            externalId,
            email,
            ParseDisplayName(displayName),
            cancellationToken);
    }

    public async Task<User> EnsureAsync(
        Guid externalId,
        string email,
        string? firstName,
        string? lastName,
        CancellationToken cancellationToken)
    {
        return await EnsureCoreAsync(
            externalId,
            email,
            CreateName(firstName, lastName),
            cancellationToken);
    }

    private async Task<User> EnsureCoreAsync(
        Guid externalId,
        string email,
        Name? name,
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
        var synchronizeDisplayName = false;

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
                    seenAt,
                    name);
                dbContext.Users.Add(user);
            }
            else
            {
                user.SynchronizeExternalIdentity(externalIdValue, email, seenAt, name);
            }

            synchronizeDisplayName = name is not null;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        if (synchronizeDisplayName && name is not null)
        {
            await TrySynchronizeDisplayNameAsync(
                externalIdValue,
                FormatDisplayName(name),
                cancellationToken);
        }

        return user ?? throw new InvalidOperationException("The member identity was not persisted.");
    }

    private static Name? ParseDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var parts = displayName
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? new Name(parts[0], string.Empty)
            : new Name(parts[0], string.Join(' ', parts.Skip(1)));
    }

    private static Name? CreateName(string? firstName, string? lastName)
    {
        var normalizedFirstName = NormalizeNamePart(firstName);
        var normalizedLastName = NormalizeNamePart(lastName);
        return normalizedFirstName is null && normalizedLastName is null
            ? null
            : new Name(normalizedFirstName ?? string.Empty, normalizedLastName ?? string.Empty);
    }

    private static string? NormalizeNamePart(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task TrySynchronizeDisplayNameAsync(
        string externalId,
        string displayName,
        CancellationToken cancellationToken)
    {
        if (directory is null)
        {
            return;
        }

        try
        {
            await directory.UpdateDisplayNameAsync(
                externalId,
                displayName,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogWarning(
                exception,
                "Unable to synchronize Entra displayName for member {ExternalId}.",
                externalId);
        }
    }

    private static string FormatDisplayName(Name name)
    {
        return string.Join(
            ' ',
            new[] { name.FirstName, name.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part.Trim()));
    }
}
