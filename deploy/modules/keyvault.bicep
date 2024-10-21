@description('The name of the Key Vault.')
param keyVaultName string

@description('The location where the Key Vault will be deployed.')
param location string

@secure()
@description('The Key Vault secret ID for the image registry username.')
param kvImageRegistryUsernameId string

@secure()
@description('The Key Vault secret ID for the image registry password.')
param kvImageRegistryPasswordId string

@secure()
@description('The image registry username stored in Key Vault.')
param kvImageRegistryUsername string

@secure()
@description('The image registry password stored in Key Vault.')
param kvImageRegistryPassword string

@description('The principal ID of the managed identity.')
param managedIdentityPrincipalId string

@description('The principal ID of the deployment user.')
param userPrincipalId string?

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

var userAccessPolicy = !empty(userPrincipalId) ? {
    tenantId: subscription().tenantId
    objectId: userPrincipalId
    permissions: {
      keys: []
      secrets: ['get', 'list', 'set', 'delete']
      certificates: []
    }
  } : {}

var managedIdentityAcessPolicy = {
  tenantId: subscription().tenantId
  objectId: managedIdentityPrincipalId
  permissions: {
    keys: []
    secrets: ['get', 'list']
    certificates: []
  }
}

var finalAccessPolicies = !empty(userPrincipalId) ? [ userAccessPolicy, managedIdentityAcessPolicy ] : [ managedIdentityAcessPolicy ]

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
    enabledForTemplateDeployment: true
    accessPolicies: finalAccessPolicies
  }
}

resource keyVaultSecrets 'Microsoft.KeyVault/vaults/secrets@2021-10-01' = [for secret in secrets: {
  parent: keyVault
  name: '${secret.name}'
  properties: {
    value: secret.value
  }
}]

output keyVaultName string = empty(keyVault) ? '' : keyVault.name
