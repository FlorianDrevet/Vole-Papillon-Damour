using Vole_Papillon_Damour.Api.Common.Utils;
using Vole_Papillon_Damour.Contracts.Events.Responses;

namespace Vole_Papillon_Damour.Api.LiveTableau;

/// <summary>One full state of a live tableau, already serialized for the SSE stream.</summary>
public sealed record LiveTableauSnapshot(int Revision, string Json)
{
    public static LiveTableauSnapshot From(EventResponse response) =>
        new(response.LiveRevision, JsonSerializerHelper.Serialize(response));
}
