param keyVaultName string
param location string
@secure()
param kvImageRegistryUsernameId string
@secure()
param kvImageRegistryPasswordId string
@secure()
param kvImageRegistryUsername string
@secure()
param kvImageRegistryPassword string
param managedIdentityPrincipalId string
param userPrincipalId string

param secrets array = [
  {
    name: kvImageRegistryUsernameId
    value: kvImageRegistryUsername
  }
  {
    name: kvImageRegistryPasswordId
    value: kvImageRegistryPassword
  }
]

resource keyVault 'Microsoft.KeyVault/vaults@2021-10-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableSoftDelete: false
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: userPrincipalId
        permissions: {
          keys: []
          secrets: ['get', 'list', 'set', 'delete']
          certificates: []
        }
      }
      {
        tenantId: subscription().tenantId
        objectId: managedIdentityPrincipalId
        permissions: {
          keys: []
          secrets: ['get', 'list']
          certificates: []
        }
      }
    ]
  }
}

resource keyVaultSecrets 'Microsoft.KeyVault/vaults/secrets@2021-10-01' = [for secret in secrets: {
  parent: keyVault
  name: '${secret.name}'
  properties: {
    value: secret.value
  }
}]
