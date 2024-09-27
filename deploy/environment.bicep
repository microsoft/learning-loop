targetScope = 'subscription'

param acrName string?
param userObjectId string?
param managedIdentityName string
param resourceGroupName string
param keyVaultName string?

param imageRegistryUsernameId string?
param imageRegistryPasswordId string?
@secure()
param imageRegistryUsername string?
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
