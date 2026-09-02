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

@description('Resource tags')
param tags object = {}

resource emailService 'Microsoft.Communication/emailServices@2026-03-18' = {
  name: name
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
  }
}

resource domain 'Microsoft.Communication/emailServices/domains@2026-03-18' = {
  parent: emailService
  name: sendingDomain
  location: 'global'
  tags: tags
  properties: {
    domainManagement: 'CustomerManaged'
    userEngagementTracking: 'Disabled'
  }
}

@description('Name of the Azure Communication Services Email resource')
output name string = emailService.name

@description('Resource ID of the Azure Communication Services Email resource')
output resourceId string = emailService.id

@description('Customer-managed sending domain')
output sendingDomain string = domain.name
