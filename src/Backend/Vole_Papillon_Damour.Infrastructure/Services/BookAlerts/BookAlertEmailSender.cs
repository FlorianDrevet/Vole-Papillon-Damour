using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

public sealed class BookAlertEmailSender : IBookAlertEmailSender
{
    private readonly BookAlertEmailOptions _options;
    private readonly EmailClient? _client;
    private readonly ILogger<BookAlertEmailSender> _logger;

    public BookAlertEmailSender(
        IOptions<BookAlertEmailOptions> options,
        ILogger<BookAlertEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
        if (!_options.Enabled)
        {
            return;
        }

        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Book alert email is enabled but its ACS endpoint must be an HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(_options.MailFrom))
        {
            throw new InvalidOperationException(
                "Book alert email is enabled but its sender address is empty.");
        }

        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(_options.ManagedIdentityClientId))
        {
            credentialOptions.ManagedIdentityClientId = _options.ManagedIdentityClientId;
        }

        _client = new EmailClient(endpoint, new DefaultAzureCredential(credentialOptions));
    }

    public bool IsEnabled => _options.Enabled;

    public async Task SendAsync(
        BookAlertDelivery delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (!IsEnabled || _client is null)
        {
            throw new InvalidOperationException("Book alert email delivery is disabled.");
        }

        var emailContent = BookAlertEmailContentBuilder.Build(
            delivery,
            _options.AssociationName,
            _options.UnsubscribeUrl);
        var content = new EmailContent(emailContent.Subject)
        {
            PlainText = emailContent.PlainText,
            Html = emailContent.Html
        };
        var message = new EmailMessage(_options.MailFrom, delivery.Email, content);
        var operation = await _client.SendAsync(
            Azure.WaitUntil.Completed,
            message,
            cancellationToken);

        _logger.LogInformation(
            "Book alert email accepted by ACS. MessageId: {MessageId}, MemberId: {MemberId}, " +
            "ItemCount: {ItemCount}, OperationId: {OperationId}",
            delivery.MessageId,
            delivery.MemberId,
            delivery.Items.Count,
            operation.Id);
    }

}
