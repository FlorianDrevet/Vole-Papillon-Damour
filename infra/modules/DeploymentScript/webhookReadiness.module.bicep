// =======================================================================
// Event Grid Webhook Readiness Module
// -----------------------------------------------------------------------
// Container App revisions load Key Vault references asynchronously. Event Grid
// validates a webhook synchronously while creating its subscription, so this
// script exercises the exact validation request until the API revision has
// loaded the secret. The Event Grid resource can then be created declaratively.
// =======================================================================

@description('Name of the deployment script')
param name string

@description('Azure region used for the temporary deployment-script resources')
param location string

@description('HTTPS endpoint receiving Event Grid validation requests')
param endpointUrl string

@description('Header name used for webhook authentication')
param webhookHeaderName string

@description('Shared secret used to authenticate the readiness request')
@secure()
@minLength(1)
param webhookSecret string

@description('Value that forces the script to execute for each infrastructure deployment')
param forceUpdateTag string

@description('Resource tags')
param tags object = {}

resource webhookReadiness 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: name
  location: location
  kind: 'AzureCLI'
  tags: tags
  properties: {
    azCliVersion: '2.50.0'
    forceUpdateTag: forceUpdateTag
    timeout: 'PT10M'
    retentionInterval: 'P1D'
    cleanupPreference: 'OnSuccess'
    environmentVariables: [
      {
        name: 'WEBHOOK_ENDPOINT'
        value: endpointUrl
      }
      {
        name: 'WEBHOOK_HEADER_NAME'
        value: webhookHeaderName
      }
      {
        name: 'WEBHOOK_SECRET'
        secureValue: webhookSecret
      }
    ]
    scriptContent: '''
set -euo pipefail

validation_code='vpd-bicep-readiness'
payload='[{"id":"vpd-bicep-readiness","eventType":"Microsoft.EventGrid.SubscriptionValidationEvent","subject":"","data":{"validationCode":"vpd-bicep-readiness"},"eventTime":"2026-01-01T00:00:00Z"}]'

for attempt in $(seq 1 60); do
  response_file=$(mktemp)
  status=$(curl --silent --show-error --output "$response_file" --write-out "%{http_code}" --max-time 10 --request POST \
    --header "Content-Type: application/json" \
    --header "aeg-event-type: SubscriptionValidation" \
    --header "$WEBHOOK_HEADER_NAME: $WEBHOOK_SECRET" \
    --data "$payload" "$WEBHOOK_ENDPOINT" || true)

  if [ "$status" = "200" ] && grep -Fq "$validation_code" "$response_file"; then
    rm -f "$response_file"
    exit 0
  fi

  rm -f "$response_file"
  sleep 10
done

echo "The API webhook did not accept Event Grid validation within 10 minutes." >&2
exit 1
'''
  }
}

output name string = webhookReadiness.name
