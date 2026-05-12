namespace Vole_Papillon_Damour.Contracts.Authentication.Requests;

public record LoginRequest
{
    public string Email { get; init; }
    public string Password { get; init; }
}
