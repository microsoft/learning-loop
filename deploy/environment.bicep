targetScope = 'subscription'

@description('The name of the Azure Container Registry (if using docker hub, leave this empty).')
param acrName string?

@description('If true, uses the supplied ACR name as is.')
param doNotGenerateACRName bool = false

@description('The Object ID of the user.')
param userObjectId string?

@description('The name of the managed identity.')
param managedIdentityName string

@description('If true, uses the supplied Managed Identity name as is.')
param doNotGenerateManagedIdentityName bool = false

@description('The name of the resource group.')
param resourceGroupName string

@description('The name of the Key Vault (leave empty to generate a key vault name).')
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

// generate a unique managed identity name from tenant id
// the name is restricted to 128 characters (3 from mi-, 111 from the supplied managed identity name, 1 hyphen, and 13 from unique string)
var generatedManagedIdentityName = 'mi-${take(managedIdentityName, 111)}-${uniqueString(tenant().tenantId)}'
var finalManagedIdentityName = doNotGenerateManagedIdentityName ? managedIdentityName : generatedManagedIdentityName

module managedIdentity './modules/managedidentity.bicep' = {
  name: 'managedIdentity'
  scope: learningLoopRg
  params: {
    name: finalManagedIdentityName
    location: deployment().location
  }
}

// generate a unique acr name from the given name and resource group name
// the name is restricted to 50 characters (3 from arc, 33 from the supplied acr name, and 13 from unique string)
var generatedAcrName = empty(acrName) ? null : 'acr${take(acrName!, 33)}${uniqueString(learningLoopRg.id)}'
var finalAcrName = doNotGenerateACRName ? acrName : generatedAcrName

module containerRegistry './modules/containerregistry.bicep' = if (!empty(acrName)) {
  name: 'containerRegistry'
  scope: learningLoopRg
  params: {
    acrName: finalAcrName!
    location: deployment().location
    roleAssignmentPrincipalId: managedIdentity.outputs.principalId
  }
}

// generate a unique key vault name if not provided and ACR is not provided (this implies a docker hub registry that requires login secrets)
// the name is restricted to 24 characters (7 from the resource group name, 1 hyphen, 13 from unique string, 1 hyphen, 2 from kv)
var generatedKeyVaultName = empty(acrName) ? '${take(learningLoopRg.name, 7)}-${uniqueString(learningLoopRg.id)}-kv' : null
var finalKeyVaultName = empty(keyVaultName) ? generatedKeyVaultName : keyVaultName!
var doCreateKeyValue = empty(acrName) && !empty(finalKeyVaultName)

module keyVault './modules/keyvault.bicep' = if (doCreateKeyValue) {
  name: 'keyVault'
  scope: learningLoopRg
  params: {
    keyVaultName: finalKeyVaultName!
    location: deployment().location
    userPrincipalId: userObjectId!
    managedIdentityPrincipalId: managedIdentity.outputs.principalId
    kvImageRegistryUsernameId: imageRegistryUsernameId!
    kvImageRegistryUsername: imageRegistryUsername!
    kvImageRegistryPasswordId: imageRegistryPasswordId!
    kvImageRegistryPassword: imageRegistryPassword!
  }
}

output keyVaultName string = doCreateKeyValue ? keyVault.outputs.keyVaultName : ''
output acrName string = !empty(acrName) ? containerRegistry.outputs.acrName : ''
output managedIdentityName string = managedIdentity.outputs.managedIdentityName
