@description('The location where the resources will be deployed')
param location string

@description('The name of the managed identity')
param name string

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: name
  location: location
}

output principalId string = managedIdentity.properties.principalId
