using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Common;

public record AssoEventResult(
    AssoEventsId Id,
    Uri UrlImage,
    string Name,
    EventsType EventsType,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    DateTimeOffset? HourOpenDoors,
    DateTimeOffset? HourCloseDoors,
    Uri? UrlRegistration,
    Uri? UrlImageMap,
    Adresse Adresse,
    string Description,
    bool BingoHasBeenWon,
    List<int> BingoNumeros,
    IReadOnlyList<PartieResult> Parties,
    int CurrentPartieIndex,
    bool IsCancelled
    );

public record PartieResult(
    PartieId Id,
    string Name,
    PartieType PartieType,
    int Index,
    bool PauseAfter,
    int? AddedBingoNumber,
    int CurrentLineIndex,
    IReadOnlyList<int> LastNumeros,
    IReadOnlyList<int> LiveNumeros,
    IReadOnlyList<LinePartieResult> LineParties
    );

public record LinePartieResult(
    LinePartieId Id,
    IReadOnlyList<LotsResult> Lots,
    NumberLine NumberLine,
    int Index
    );

public record LotsResult(
    LotId Id,
    string Name,
    string UrlImage,
    int Index,
    int? IsWon
    );
