namespace Vole_Papillon_Damour.Contracts.Authentication.Requests;

public class RegisterRequest
{
    public string Email { get; init; }
    public string Password { get; init; }
    public string Firstname { get; init; }
    public string Lastname { get; init; }
}