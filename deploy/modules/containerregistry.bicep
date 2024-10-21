// Deploys a container registry and grants the specified principal the "AcrPull" role.
// If existing resouce is specified, it will assign the role to the existing acr.
@description('The location where the resources will be deployed.')
param location string

@description('The name of the Azure Container Registry.')
param acrName string

@description('Create the Acr or retrieve an existing acr and assign the role.')
param createAcr bool

@description('The principal ID for the role assignment.')
param roleAssignmentPrincipalId string

// NOTE: normally you wouldn't check for existence of the resource, but we want to make sure we don't modify the existing ACR in this case

resource newContainerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = if (createAcr) {
  name: acrName
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    adminUserEnabled: false
  }
}

resource newAcrRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (createAcr) {
  name: guid(newContainerRegistry.id, 'AcrPull', acrName)
  scope: newContainerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: roleAssignmentPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource existingContainerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' existing = if (!createAcr) {
  name: acrName
}

resource existingAcrRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (!createAcr) {
  name: guid(existingContainerRegistry.id, 'AcrPull', acrName)
  scope: existingContainerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: roleAssignmentPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output acrName string = createAcr ? newContainerRegistry.name : existingContainerRegistry.name
