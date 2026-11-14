namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

/// <summary>
/// Mints and verifies the per-recipient token carried by the one-click
/// unsubscribe URL (RFC 8058). The URL reaches mailbox providers, so the token
/// is the only thing standing between a delivery report and an unsubscription:
/// it must be unguessable and bound to a single member.
/// </summary>
public interface IUnsubscribeTokenService
{
    string Create(Guid memberId, UnsubscribePurpose purpose = UnsubscribePurpose.BookAlerts);

    bool TryValidate(string token, out Guid memberId, out UnsubscribePurpose purpose);

    bool TryValidate(string token, out Guid memberId) =>
        TryValidate(token, out memberId, out _);

    bool TryValidate(string token, UnsubscribePurpose expectedPurpose, out Guid memberId) =>
        TryValidate(token, out memberId, out var purpose) && purpose == expectedPurpose;
}
