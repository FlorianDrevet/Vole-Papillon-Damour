namespace Vole_Papillon_Damour.Contracts.Authentication.Responses;

public record AuthenticationResponse (
    Guid Id,
    string Email,
    string Token);