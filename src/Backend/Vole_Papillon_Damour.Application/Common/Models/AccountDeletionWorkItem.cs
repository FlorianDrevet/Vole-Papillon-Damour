namespace Vole_Papillon_Damour.Application.Common.Models;

public sealed record AccountDeletionWorkItem(
    Guid RequestId,
    Guid? UserId,
    string ExternalId);
