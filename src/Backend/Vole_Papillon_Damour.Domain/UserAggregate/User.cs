using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.UserAggregate;

public sealed class User : AggregateRoot<UserId>
{
    public string? ExternalId { get; private set; }
    public string? Email { get; private set; }
    public Name? Name { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; private set; } = DateTime.UtcNow;
    public DateTime? AnonymizedAt { get; private set; }

    // Kept only as a source-compatible bridge until L0-11 step 5 removes the
    // legacy authentication path. UserConfiguration deliberately ignores them.
    public string Password { get; private set; } = null!;
    public string Salt { get; private set; } = null!;
    public string Role { get; set; } = null!;

    private User(UserId userId, string email, string password, Name name, string salt) 
        : base(userId)
    {
        Email = email;
        Password = password;
        Name = name;
        Salt = salt;
        Role = "User";

        var now = DateTime.UtcNow;
        CreatedAt = now;
        LastSeenAt = now;
    }
    
    public static User Create(string email, string password, Name name, string salt)
    {
        return new User(UserId.CreateUnique(), email, password, name, salt);
    }
    
    public User(){}

    public void ChangeEmail(string requestEmail)
    {
        Email = requestEmail;
    }

    public void Anonymize(DateTime anonymizedAt)
    {
        ExternalId = null;
        Email = null;
        Name = null;
        AnonymizedAt = anonymizedAt;
    }
}
