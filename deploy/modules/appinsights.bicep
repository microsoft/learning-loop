// Deploys an Application Insights instance to a given location.
// Use this module to either create an Application Insights instance in
// a new or existing resource group or use an existing one.
@description('The location where the Key Vault will be deployed.')
param location string

@description('The name of the Application Insights instance.')
param insightsName string

@description('If true, a unique name will be generated for the Application Insights instance.')
param generateName bool

@description('If true, the Application Insights instance will be created; otherwise, the existing resource will be used.')
param create bool

// NOTE: normally you wouldn't check for existence of the resource, but we want to make sure we don't modify the existing reosurce in this case

// generate a unique application insights name from subscription id and the location
// the name is restricted to 255 characters (3 from ai-, 128 from the supplied managed identity name, 1 hyphen, and 13 from unique string)
var finalAppInsightsName =  generateName ? 'ai-${take(insightsName, 128)}-${uniqueString(subscription().tenantId, location)}' : insightsName

resource newAppInsights 'Microsoft.Insights/components@2020-02-02' = if (create) {
  name: finalAppInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource existingAppInsights 'Microsoft.Insights/components@2020-02-02' existing = if (!create) {
  name: finalAppInsightsName
}

output applicationInsightsConnectionString string = create ? newAppInsights.properties.ConnectionString : existingAppInsights.properties.ConnectionString
output applicationInsightsInstrumentationKey string = create ? newAppInsights.properties.InstrumentationKey : existingAppInsights.properties.InstrumentationKey
