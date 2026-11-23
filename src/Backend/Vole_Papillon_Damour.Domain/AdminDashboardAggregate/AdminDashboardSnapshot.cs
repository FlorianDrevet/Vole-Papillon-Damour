using Vole_Papillon_Damour.Domain.Common;

namespace Vole_Papillon_Damour.Domain.AdminDashboardAggregate;

/// <summary>Ligne unique (Id = 1) : compte pré-calculé des titres jamais vendus.</summary>
public sealed class AdminDashboardSnapshot
{
    public const byte SingletonId = 1;

    public byte Id { get; private set; } = SingletonId;
    public int DeadStockCount { get; private set; }
    public int DeadStockMinAgeDays { get; private set; }
    public int DeadStockMinQuantity { get; private set; }
    public DateTime ComputedAt { get; private set; }
    public string? IsbndbUnmatchedSubjectsJson { get; private set; }
    public DateTime? IsbndbUnmatchedSubjectsComputedAt { get; private set; }

    private AdminDashboardSnapshot()
    {
    }

    public static AdminDashboardSnapshot Create(
        int deadStockCount,
        int deadStockMinAgeDays,
        int deadStockMinQuantity,
        DateTime computedAt)
    {
        var snapshot = new AdminDashboardSnapshot();
        snapshot.Refresh(deadStockCount, deadStockMinAgeDays, deadStockMinQuantity, computedAt);
        return snapshot;
    }

    public void Refresh(
        int deadStockCount,
        int deadStockMinAgeDays,
        int deadStockMinQuantity,
        DateTime computedAt)
    {
        if (deadStockCount < 0 || deadStockMinAgeDays < 0 || deadStockMinQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deadStockCount), "Dashboard snapshot values cannot be negative.");
        }

        DeadStockCount = deadStockCount;
        DeadStockMinAgeDays = deadStockMinAgeDays;
        DeadStockMinQuantity = deadStockMinQuantity;
        ComputedAt = DomainTime.RequireUtc(computedAt, nameof(computedAt));
    }

    public void RefreshIsbndbUnmatchedSubjects(string subjectsJson, DateTime computedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectsJson);
        IsbndbUnmatchedSubjectsJson = subjectsJson;
        IsbndbUnmatchedSubjectsComputedAt = DomainTime.RequireUtc(computedAt, nameof(computedAt));
    }
}
