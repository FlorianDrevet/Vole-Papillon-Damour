namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IRandomIndexSource
{
    int Next(int exclusiveMaximum);
}
