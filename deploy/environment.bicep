targetScope = 'subscription'

@description('The name of the Azure Container Registry (if using docker hub, leave this empty).')
param acrName string?

@description('The Object ID of the user.')
param userObjectId string?

@description('The name of the managed identity.')
param managedIdentityName string

@description('The name of the resource group.')
param resourceGroupName string

@description('The name of the Key Vault.')
param keyVaultName string?

@description('The ID of the Key Vault secret containing the image registry username.')
param imageRegistryUsernameId string?

@description('The ID of the Key Vault secret containing the image registry password.')
param imageRegistryPasswordId string?

@description('The username for the image registry.')
@secure()
param imageRegistryUsername string?

@description('The password for the image registry.')
@secure()
param imageRegistryPassword string?

resource learningLoopRg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resourceGroupName
  location: deployment().location
}

module managedIdentity './modules/managedidentity.bicep' = {
  name: 'managedIdentity'
  scope: learningLoopRg
  params: {
    name: managedIdentityName
    location: deployment().location
  }
}

module containerRegistry './modules/containerregistry.bicep' = if (!empty(acrName)) {
  name: 'containerRegistry'
  scope: learningLoopRg
  params: {
    acrName: acrName!
    location: deployment().location
    roleAssignmentPrincipalId: managedIdentity.outputs.principalId
  }
}

module keyVault './modules/keyvault.bicep' = if (!empty(keyVaultName) && empty(acrName)) {
  name: 'keyVault'
  scope: learningLoopRg
  params: {
    keyVaultName: keyVaultName!
    location: deployment().location
    userPrincipalId: userObjectId!
    managedIdentityPrincipalId: managedIdentity.outputs.principalId
    kvImageRegistryUsernameId: imageRegistryUsernameId!
    kvImageRegistryUsername: imageRegistryUsername!
    kvImageRegistryPasswordId: imageRegistryPasswordId!
    kvImageRegistryPassword: imageRegistryPassword!
  }
}
