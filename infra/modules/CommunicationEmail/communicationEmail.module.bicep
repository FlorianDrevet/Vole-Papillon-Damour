// =======================================================================
// Azure Communication Services Email module
// -----------------------------------------------------------------------
// Creates the Email service and its customer-managed sending domain.
// Azure exposes the verification TXT/SPF/DKIM values after the domain is
// provisioned; they must not be guessed or embedded in this template.
// See: https://learn.microsoft.com/en-us/azure/templates/microsoft.communication/2026-03-18/emailservices
// =======================================================================

@description('Name of the Azure Communication Services Email resource')
param name string

@description('ACS data-residency geography, for example France or Europe')
param dataLocation string

@description('Customer-managed domain used for sending email')
param sendingDomain string

@description('Create the email service and domain. Keep false after the first bootstrap so ACS verification state is not reset by subsequent deployments.')
param createResources bool = false

@description('Resource tags')
param tags object = {}

resource emailService 'Microsoft.Communication/emailServices@2026-03-18' = if (createResources) {
  name: name
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
  }
}

resource domain 'Microsoft.Communication/emailServices/domains@2026-03-18' = if (createResources) {
  parent: emailService
  name: sendingDomain
  location: 'global'
  tags: tags
  properties: {
    domainManagement: 'CustomerManaged'
    userEngagementTracking: 'Disabled'
  }
}

// When createResources is false the resources are intentionally not emitted:
// ARM receives no PUT and therefore keeps ACS-managed DNS verification state.
// The IDs remain deterministic and are consumed by the Communication Service
// module, which fails clearly if the bootstrap resources are missing.
@description('Name of the Azure Communication Services Email resource')
output name string = name

@description('Resource ID of the Azure Communication Services Email resource')
output resourceId string = resourceId('Microsoft.Communication/emailServices', name)

@description('Resource ID of the customer-managed sending domain')
output domainResourceId string = resourceId('Microsoft.Communication/emailServices/domains', name, sendingDomain)

@description('Customer-managed sending domain')
output sendingDomain string = sendingDomain
