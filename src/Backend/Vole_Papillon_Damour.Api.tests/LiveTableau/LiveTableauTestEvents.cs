using Mapster;
using MapsterMapper;
using Vole_Papillon_Damour.Api.Common.Mapping;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.LiveTableau;

internal static class LiveTableauTestEvents
{
    /// <summary>
    /// The API mapping, as in production: <see cref="EventMappingConfig"/> registers
    /// some of its conversions on the global settings.
    /// </summary>
    public static IMapper CreateMapper()
    {
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(typeof(EventMappingConfig).Assembly);
        return new Mapper(config);
    }

    public static AssoEvents CreateLoto()
    {
        var partie = Partie.Create(
            "Panier gourmand",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            0,
            false,
            [LinePartie.Create([Lot.Create("Panier garni", "lot.jpg", 0)], new NumberLine(NumberLine.NumberLineEnum.OneLine), 0)]);

        return AssoEvents.Create(
            "Grand loto",
            new Uri("https://example.com/loto.jpg"),
            new EventsType(EventsType.EventsTypeEnum.Bingo),
            new DateTimeOffset(2026, 11, 15, 13, 0, 0, TimeSpan.Zero),
            null,
            null,
            null,
            null,
            new Adresse(12, "Arras", "Rue du loto", 62000),
            null,
            [partie],
            "Loto");
    }
}
