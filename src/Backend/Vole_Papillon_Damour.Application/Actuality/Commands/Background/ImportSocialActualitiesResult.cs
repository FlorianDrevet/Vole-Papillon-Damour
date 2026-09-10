namespace Vole_Papillon_Damour.Application.Actuality.Commands.Background;

public sealed record ImportSocialActualitiesResult(
    int ExaminedCount,
    int ImportedCount,
    int AlreadyKnownCount,
    int FailedCount,
    int NoMediaCount,
    int FallbackTitleCount);
