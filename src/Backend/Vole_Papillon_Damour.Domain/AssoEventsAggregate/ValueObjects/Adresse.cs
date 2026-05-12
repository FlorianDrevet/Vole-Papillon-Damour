using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

public sealed class Adresse(int? roadNumber, string city, string road, int cityCode) : ValueObject
{
    public int? RoadNumber { get; protected set; } = roadNumber;
    public string City { get; protected set; } = city;
    public int CityCode { get; protected set; } = cityCode;
    public string Road { get; protected set; } = road;

    public override IEnumerable<object> GetEqualityComponents()
    {
        if (RoadNumber is not null)
        {
            yield return RoadNumber;
        }
        yield return City;
        yield return CityCode;
        yield return Road;
    }
}