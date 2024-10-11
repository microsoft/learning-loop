// Create or retrieve a managed identity
@description('The location where the resources will be deployed')
param location string

@description('The name of the managed identity')
param name string

param createManagedIdentity bool = true

resource newManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = if (createManagedIdentity) {
  name: name
  location: location
}

resource existingManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = if (!createManagedIdentity) {
  name: name
}

output principalId string = createManagedIdentity ? newManagedIdentity.properties.principalId : existingManagedIdentity.properties.principalId
output managedIdentityName string = createManagedIdentity ? newManagedIdentity.name : existingManagedIdentity.name
