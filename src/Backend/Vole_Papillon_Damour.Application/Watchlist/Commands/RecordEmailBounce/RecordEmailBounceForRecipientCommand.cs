using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RecordEmailBounce;

public sealed record RecordEmailBounceForRecipientCommand(
    string Recipient,
    string ProviderEventId) : IRequest<ErrorOr<RecordEmailBounceForRecipientResult>>;
