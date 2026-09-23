namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IMemberCardTokenService
{
    string Create(Guid cardId, int version);

    bool TryRead(string? token, out Guid cardId, out int version);
}
