using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.Services.FairDigest;

public sealed class FairDigestEmailSender : IFairDigestEmailSender
{
    private readonly BookAlertEmailOptions _options;
    private readonly UnsubscribeTokenOptions _unsubscribeOptions;
    private readonly IUnsubscribeTokenService _unsubscribeTokenService;
    private readonly IFairDigestEmailRenderer _renderer;
    private readonly EmailClient? _client;
    private readonly ILogger<FairDigestEmailSender> _logger;

    public FairDigestEmailSender(
        IOptions<BookAlertEmailOptions> options,
        IOptions<UnsubscribeTokenOptions> unsubscribeOptions,
        IUnsubscribeTokenService unsubscribeTokenService,
        IFairDigestEmailRenderer renderer,
        ILogger<FairDigestEmailSender> logger)
    {
        _options = options.Value;
        _unsubscribeOptions = unsubscribeOptions.Value;
        _unsubscribeTokenService = unsubscribeTokenService;
        _renderer = renderer;
        _logger = logger;
        if (!_options.Enabled)
        {
            return;
        }

        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Fair digest email is enabled but its ACS endpoint must be an HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(_options.MailFrom))
        {
            throw new InvalidOperationException(
                "Fair digest email is enabled but its sender address is empty.");
        }

        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(_options.ManagedIdentityClientId))
        {
            credentialOptions.ManagedIdentityClientId = _options.ManagedIdentityClientId;
        }

        _client = new EmailClient(endpoint, new DefaultAzureCredential(credentialOptions));
    }

    public bool IsEnabled => _options.Enabled;

    public async Task SendAsync(FairDigestEmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!IsEnabled || _client is null)
        {
            throw new InvalidOperationException("Fair digest email delivery is disabled.");
        }

        var token = _unsubscribeTokenService.Create(message.MemberId.Value, UnsubscribePurpose.FairDigest);
        var emailContent = _renderer.Render(message, token);
        var content = new EmailContent(emailContent.Subject)
        {
            PlainText = emailContent.PlainText,
            Html = emailContent.Html
        };
        var email = new EmailMessage(_options.MailFrom, message.Email, content);
        foreach (var header in BookAlertEmailHeaders.BuildOneClickUnsubscribe(
                     _unsubscribeOptions.Endpoint,
                     token))
        {
            email.Headers.Add(header.Key, header.Value);
        }

        var operation = await _client.SendAsync(Azure.WaitUntil.Completed, email, cancellationToken);
        _logger.LogInformation(
            "Fair digest email accepted by ACS. CampaignId: {CampaignId}, MemberId: {MemberId}, " +
            "NewsCount: {NewsCount}, BackCount: {BackCount}, OperationId: {OperationId}",
            message.CampaignId,
            message.MemberId,
            message.Content.News.Count,
            message.Content.Back.Count,
            operation.Id);
    }
}
