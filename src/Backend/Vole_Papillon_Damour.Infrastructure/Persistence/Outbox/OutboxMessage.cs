namespace Vole_Papillon_Damour.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public OutboxMessageKind Kind { get; set; }
    public string PayloadJson { get; set; } = null!;
    public DateTime DueAt { get; set; }
    public OutboxMessageStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTime? ClaimedUntil { get; set; }
    public Guid? ScanSessionId { get; set; }
    public Guid? MemberId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
}

public enum OutboxMessageKind : byte
{
    AccountDeletion = 0
}

public enum OutboxMessageStatus : byte
{
    Pending = 0,
    Sent = 1,
    Cancelled = 2,
    Failed = 3
}
