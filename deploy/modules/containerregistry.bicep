@description('The location where the resources will be deployed.')
param location string

@description('The name of the Azure Container Registry.')
param acrName string

@description('The principal ID for the role assignment.')
param roleAssignmentPrincipalId string

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: acrName
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    adminUserEnabled: false
  }
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(containerRegistry.id, 'AcrPull', acrName)
  scope: containerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: roleAssignmentPrincipalId
    principalType: 'ServicePrincipal'
  }
}
