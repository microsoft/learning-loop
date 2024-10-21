// Template for creating the resources needed for a Learning Loop deployment using an Azure Container Registry.
// This includes the resource group, managed identity, container registry, and application insights resources.
// The output of this module include:
//    resourceGroupName - the resource group name
//    managedIdentityName - the name of the managed identity
//    acrName - the name of the acr (if an acr is used)
//    appInsightsConnectionString - the connection string for the application insights resource (if app insights is used)
// The outputs can be used to setup the Learning Loop application deployment using acr-main.bicep.
targetScope = 'subscription'

@description('The new resource group for the learning loop deployment.')
param resourceGroupName string = 'rg-sample-loop'

@description('The managed identity name for learning loop role assignments.')
param managedIdentityName string = 'mi-sample-loop-${uniqueString(tenant().tenantId)}'

@description('The Azure Container Registry name.')
param acrName string = 'acrsampleloop${uniqueString(tenant().tenantId)}'

@description('Application Insights name.')
param appInsightsName string = 'ai-metrics-${uniqueString(tenant().tenantId)}'

// create the resource in the deployment location
resource learningLoopRg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resourceGroupName
  location: deployment().location
}

module managedIdentity './modules/managedidentity.bicep' = {
  name: 'managedIdentity'
  scope: learningLoopRg
  params: {
    name: managedIdentityName
    createManagedIdentity: true
    location: deployment().location
  }
}

module containerRegistry './modules/containerregistry.bicep' = {
  name: 'containerRegistry'
  scope: learningLoopRg
  params: {
    createAcr: true
    acrName: acrName
    location: deployment().location
    roleAssignmentPrincipalId: managedIdentity.outputs.principalId
  }
}

// setup metrics if needed
module appInsights './modules/appinsights.bicep' = {
  name: 'appInsights'
  scope: learningLoopRg
  params: {
    create: true
    generateName: true
    insightsName: appInsightsName
    location: deployment().location
  }
}

output resourceGroupName string = learningLoopRg.name
output managedIdentityName string = managedIdentity.outputs.managedIdentityName
output acrName string = containerRegistry.outputs.acrName
output appInsightsConnectionString string = appInsights.outputs.applicationInsightsConnectionString
