using System.Text.Json.Serialization;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Contracts.MemberSelection;

public sealed record ReportNotFoundRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter<NotFoundReportLocation>))]
    NotFoundReportLocation? Location = null,
    string? Comment = null);
