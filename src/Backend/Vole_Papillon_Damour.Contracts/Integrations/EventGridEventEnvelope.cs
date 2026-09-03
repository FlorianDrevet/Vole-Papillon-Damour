using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vole_Papillon_Damour.Contracts.Integrations;

public sealed record EventGridEventEnvelope(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("eventType")] string? EventType,
    [property: JsonPropertyName("data")] JsonElement Data,
    [property: JsonPropertyName("eventTime")] DateTimeOffset? EventTime);

public sealed record EventGridSubscriptionValidationData(
    [property: JsonPropertyName("validationCode")] string? ValidationCode);

public sealed record EventGridValidationResponse(
    [property: JsonPropertyName("validationResponse")] string ValidationResponse);

public sealed record AcsEmailDeliveryReportData(
    [property: JsonPropertyName("sender")] string? Sender,
    [property: JsonPropertyName("recipient")] string? Recipient,
    [property: JsonPropertyName("messageId")] string? MessageId,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("deliveryAttemptTimestamp")] DateTimeOffset? DeliveryAttemptTimestamp,
    [property: JsonPropertyName("deliveryStatusDetails")] AcsEmailDeliveryStatusDetails? DeliveryStatusDetails);

public sealed record AcsEmailDeliveryStatusDetails(
    [property: JsonPropertyName("statusMessage")] string? StatusMessage);
