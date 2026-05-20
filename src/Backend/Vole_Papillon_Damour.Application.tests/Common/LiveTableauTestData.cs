using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Common;

internal static class LiveTableauTestData
{
    public static AssoEvents CreateEvent(params Partie[] parties)
    {
        return AssoEvents.Create(
            "Live loto",
            new Uri("https://example.com/loto.jpg"),
            new EventsType(EventsType.EventsTypeEnum.Bingo),
            new DateTimeOffset(2026, 5, 19, 20, 0, 0, TimeSpan.Zero),
            null,
            null,
            null,
            null,
            new Adresse(12, "Arras", "Rue du loto", 62000),
            null,
            parties,
            "Live loto test event");
    }

    public static Partie CreatePartie(
        int index,
        PartieType.PartieTypeEnum partieType = PartieType.PartieTypeEnum.Standard,
        int lineCount = 1)
    {
        var lineParties = Enumerable.Range(0, lineCount)
            .Select(lineIndex => LinePartie.Create(
                [Lot.Create($"Lot {lineIndex + 1}", $"lot-{lineIndex + 1}.jpg", lineIndex)],
                new NumberLine((NumberLine.NumberLineEnum)lineIndex),
                lineIndex))
            .ToList();

        return Partie.Create(
            $"Partie {index}",
            new PartieType(partieType),
            index,
            false,
            lineParties);
    }

    public static void DrawNumeroAddingBingoNumber(AssoEvents assoEvent, Partie partie, int numero)
    {
        partie.AddLiveNumero(numero);
        if (assoEvent.AddBingoNumero(numero))
        {
            partie.AddedBingoNumber = numero;
        }
    }
}
