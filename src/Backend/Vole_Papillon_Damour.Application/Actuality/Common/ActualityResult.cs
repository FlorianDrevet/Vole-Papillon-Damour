using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Actuality.Common;

public record ActualityResult(string Title,
    string Article,
    Uri UrlPrincipalImage,
    Uri FacebookLink, 
    Uri InstagramLink,
    List<Uri> Images,
    DateTimeOffset Date,
    Guid Id);