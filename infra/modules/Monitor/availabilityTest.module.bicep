// =======================================================================
// Application Insights standard availability test module
// -----------------------------------------------------------------------
// Probes a public URL from several Azure regions and alerts when at least
// two regions fail in the same window. This is the only signal that works
// when nobody is using the application: no traffic means no failed request.
// Billing: one execution per location per run (about EUR 0.0005 each).
// =======================================================================

@description('Name of the web test resource')
param name string

@description('Display name shown in Application Insights')
param displayName string

@description('Azure region of the Application Insights component')
param location string

@description('Resource ID of the Application Insights component that stores the results')
param applicationInsightsId string

@description('HTTPS URL probed by the test')
param url string

@description('Resource ID of the action group notified on failure')
param actionGroupId string

@description('Whether the synthetic test and its alert are active')
param enabled bool = true

@description('Seconds between two runs from each location')
@allowed([
  300
  600
  900
])
param frequencySeconds int = 900

@description('Availability test location IDs')
param locations string[] = [
  'emea-fr-pra-edge' // France Central
  'emea-nl-ams-azr' // West Europe
  'emea-gb-db3-azr' // North Europe
]

@description('Alert severity from 0 (critical) to 4 (verbose)')
param severity int = 1

@description('Resource tags')
param tags object = {}

resource webTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: name
  location: location
  // The hidden-link tag is what attaches the test to its Application Insights component.
  tags: union(tags, {
    'hidden-link:${applicationInsightsId}': 'Resource'
  })
  kind: 'standard'
  properties: {
    SyntheticMonitorId: name
    Name: displayName
    Enabled: enabled
    Frequency: frequencySeconds
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: [for locationId in locations: {
      Id: locationId
    }]
    Request: {
      RequestUrl: url
      HttpVerb: 'GET'
      ParseDependentRequests: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      IgnoreHttpStatusCode: false
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
    }
  }
}

resource availabilityAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${name}-alert'
  location: 'global'
  tags: tags
  properties: {
    description: '${displayName}: at least two test locations failed.'
    severity: severity
    enabled: enabled
    scopes: [
      webTest.id
      applicationInsightsId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: webTest.id
      componentId: applicationInsightsId
      failedLocationCount: 2
    }
    autoMitigate: true
    actions: [
      {
        actionGroupId: actionGroupId
      }
    ]
  }
}
