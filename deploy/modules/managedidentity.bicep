param location string
param name string

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: name
  location: location
}

output principalId string = managedIdentity.properties.principalId
