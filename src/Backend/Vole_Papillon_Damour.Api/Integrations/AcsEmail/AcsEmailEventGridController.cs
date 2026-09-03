using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RecordEmailBounce;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Contracts.Integrations;

namespace Vole_Papillon_Damour.Api.Integrations.AcsEmail;

public static class AcsEmailEventGridController
{
    private const string SubscriptionValidationEventType =
        "Microsoft.EventGrid.SubscriptionValidationEvent";
    private const string DeliveryReportEventType =
        "Microsoft.Communication.EmailDeliveryReportReceived";

    private static readonly JsonSerializerOptions EventGridJsonOptions =
        new(JsonSerializerDefaults.Web);

    public static IApplicationBuilder UseAcsEmailEventGridController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapPost(
                    "/integrations/acs/email-delivery-reports",
                    async (
                        HttpContext httpContext,
                        IOptions<EmailBounceWebhookOptions> options,
                        IMediator mediator,
                        CancellationToken cancellationToken) =>
                    {
                        var webhookOptions = options.Value;
                        if (string.IsNullOrWhiteSpace(webhookOptions.SharedSecret))
                        {
                            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                        }

                        if (!HasValidSecret(httpContext, webhookOptions.SharedSecret))
                        {
                            return Results.Unauthorized();
                        }

                        List<EventGridEventEnvelope>? events;
                        try
                        {
                            events = await JsonSerializer.DeserializeAsync<List<EventGridEventEnvelope>>(
                                httpContext.Request.Body,
                                EventGridJsonOptions,
                                cancellationToken);
                        }
                        catch (JsonException)
                        {
                            return Results.BadRequest();
                        }

                        if (events is null || events.Count == 0)
                        {
                            return Results.BadRequest();
                        }

                        if (events.Count == 1 &&
                            string.Equals(
                                events[0].EventType,
                                SubscriptionValidationEventType,
                                StringComparison.Ordinal))
                        {
                            if (!TryDeserialize(
                                    events[0].Data,
                                    out EventGridSubscriptionValidationData? validationData) ||
                                string.IsNullOrWhiteSpace(validationData?.ValidationCode))
                            {
                                return Results.BadRequest();
                            }

                            return Results.Ok(new EventGridValidationResponse(validationData.ValidationCode));
                        }

                        if (events.Any(eventGridEvent => string.Equals(
                                eventGridEvent.EventType,
                                SubscriptionValidationEventType,
                                StringComparison.Ordinal)))
                        {
                            return Results.BadRequest();
                        }

                        var reports = new List<(string ProviderEventId, AcsEmailDeliveryReportData Data)>();
                        foreach (var eventGridEvent in events)
                        {
                            if (!string.Equals(
                                    eventGridEvent.EventType,
                                    DeliveryReportEventType,
                                    StringComparison.Ordinal))
                            {
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(eventGridEvent.Id) ||
                                !TryDeserialize(
                                    eventGridEvent.Data,
                                    out AcsEmailDeliveryReportData? report) ||
                                string.IsNullOrWhiteSpace(report?.Recipient) ||
                                string.IsNullOrWhiteSpace(report.Status))
                            {
                                return Results.BadRequest();
                            }

                            if (IsFailure(report.Status))
                            {
                                reports.Add((eventGridEvent.Id, report));
                            }
                        }

                        var processedCount = 0;
                        var ignoredCount = 0;
                        foreach (var report in reports)
                        {
                            var result = await mediator.Send(
                                new RecordEmailBounceForRecipientCommand(
                                    report.Data.Recipient!,
                                    report.ProviderEventId),
                                cancellationToken);

                            if (result.IsError)
                            {
                                return result.Errors.First().Result();
                            }

                            if (result.Value.Outcome is
                                RecordEmailBounceForRecipientOutcome.Recorded or
                                RecordEmailBounceForRecipientOutcome.AlreadyRecorded)
                            {
                                processedCount++;
                            }
                            else
                            {
                                ignoredCount++;
                            }
                        }

                        return Results.Ok(new
                        {
                            processedCount,
                            ignoredCount
                        });
                    })
                .WithName("ReceiveAcsEmailDeliveryReport")
                .AllowAnonymous();
        });
    }

    private static bool HasValidSecret(HttpContext httpContext, string expectedSecret)
    {
        var providedSecret = httpContext.Request.Headers[EmailBounceWebhookOptions.SharedSecretHeaderName]
            .ToString();
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);
        var providedBytes = Encoding.UTF8.GetBytes(providedSecret);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static bool TryDeserialize<T>(JsonElement data, out T? value)
    {
        try
        {
            value = data.Deserialize<T>(EventGridJsonOptions);
            return value is not null;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
        catch (InvalidOperationException)
        {
            value = default;
            return false;
        }
    }

    private static bool IsFailure(string status)
    {
        return !string.Equals(status, "Delivered", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(status, "Expanded", StringComparison.OrdinalIgnoreCase);
    }
}
