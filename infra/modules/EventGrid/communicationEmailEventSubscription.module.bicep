// =======================================================================
// ACS Email Event Grid Subscription Module
// -----------------------------------------------------------------------
// Subscribes to ACS delivery reports and sends them to the API webhook with a
// static secret header. The secret is declared secure so it does not appear in
// deployment outputs or the generated template parameters.
// =======================================================================

@description('Name of the Event Grid subscription')
param name string

@description('Name of the parent Azure Communication Service')
param communicationServiceName string

@description('HTTPS endpoint receiving Event Grid validation and delivery reports')
@secure()
param endpointUrl string

@description('Header name used for webhook authentication')
param webhookHeaderName string

@description('Shared secret sent in the webhook authentication header')
@secure()
@minLength(1)
param webhookSecret string

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' existing = {
  name: communicationServiceName
}

resource eventSubscription 'Microsoft.EventGrid/eventSubscriptions@2023-12-15-preview' = {
  name: name
  scope: communicationService
  properties: {
    eventDeliverySchema: 'EventGridSchema'
    filter: {
      includedEventTypes: [
        'Microsoft.Communication.EmailDeliveryReportReceived'
      ]
    }
    destination: {
      endpointType: 'WebHook'
      properties: {
        endpointUrl: endpointUrl
        minimumTlsVersionAllowed: '1.2'
        maxEventsPerBatch: 1
        preferredBatchSizeInKilobytes: 64
        deliveryAttributeMappings: [
          {
            name: webhookHeaderName
            type: 'Static'
            properties: {
              isSecret: true
              value: webhookSecret
            }
          }
        ]
      }
    }
    retryPolicy: {
      maxDeliveryAttempts: 30
      eventTimeToLiveInMinutes: 1440
    }
  }
}

output resourceId string = eventSubscription.id
