using System.Security.Cryptography;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.MemberCards;

public sealed class CryptographicRandomIndexSource : IRandomIndexSource
{
    public int Next(int exclusiveMaximum) => RandomNumberGenerator.GetInt32(exclusiveMaximum);
}
