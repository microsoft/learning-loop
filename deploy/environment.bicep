// Create a base Learning Loop environment for a Loop deployment.
// This includes the resource group, managed identity, container registry, key vault, and application insights resources.
// The output of this module include:
//    loopName - the loop name
//    resourceGroupName - the resource group name
//    keyVaultName - the key vault name (if a keyvault is used)
//    acrName - the name of the acr (if an acr is used)
//    managedIdentityName - the name of the managed identity
//    appInsightsConnectionString - the connection string for the application insights resource (if app insights is used)
//    appInsightsInstrumentationKey - the instrumentation key for the application insights resource (if app insights is used)
//    loopDeploymentParams - the deployment parameters for the loop deployment (bicepparams)
//    imageHost - the host for the image registry
//    imageName - the name of the image
//    imageTag - the tag of the image
// The outputs can be used to setup the Learning Loop deployment.
import * as conf from './modules/environmentconfigtypes.bicep'
targetScope = 'subscription'

@description('Environment setup for the deployment')
param config conf.environmentConfigT

@description('The user object id to assign the role to (overrides params file value).')
param userObjectIdOverride string = ''

@description('The username for the image registry (overrides params file value).')
@secure()
param imageRegistryUsername string?

@description('The password for the image registry (overrides params file value).')
@secure()
param imageRegistryPassword string?

// create the resource in the deployment location
resource learningLoopRg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: config.resourceGroupName
  location: deployment().location
}

// determine with user object id to use for role assignment
var userRoleAssignmentPrincipalId = empty(userObjectIdOverride) ? config.userRoleAssignmentPrincipalId : userObjectIdOverride

// generate a unique managed identity name from tenant id
// the name is restricted to 128 characters (3 from mi-, 111 from the supplied managed identity name, 1 hyphen, and 13 from unique string)
var generatedManagedIdentityName = 'mi-${take(config.managedIdentityName, 111)}-${uniqueString(tenant().tenantId)}'
var finalManagedIdentityName = config.generateManagedIdentityName ? generatedManagedIdentityName : config.managedIdentityName

module managedIdentity './modules/managedidentity.bicep' = {
  name: 'managedIdentity'
  scope: learningLoopRg
  params: {
    name: finalManagedIdentityName
    createManagedIdentity: config.createManagedIdentity
    location: deployment().location
  }
}

// generate a unique acr name from the given name and resource group name
// the name is restricted to 50 characters (3 from arc, 33 from the supplied acr name, and 13 from unique string)
var useAcr = config.image.properties.kind == 'acr'
var generateAcrRepoName = (useAcr && config.image.properties.createAcr && config.image.properties.generateRepoName)
var finalAcrName = generateAcrRepoName ? 'acr${take(config.image.properties.repositoryName, 33)}${uniqueString(learningLoopRg.id)}' : config.image.properties.repositoryName

module containerRegistry './modules/containerregistry.bicep' = if (useAcr) {
  name: 'containerRegistry'
  scope: learningLoopRg
  params: {
    createAcr: config.image.properties.createAcr
    acrName: finalAcrName!
    location: deployment().location
    roleAssignmentPrincipalId: managedIdentity.outputs.principalId
  }
}

// setup the key vault if needed
var useKeyVault = config.image.properties.kind != 'acr' && !empty(config.image.properties.credentials!.keyVault)
var generateKeyVaultName = useKeyVault && config.image.properties.credentials!.keyVault!.createKeyVault
// generate a unique key vault name if not provided and ACR is not provided (this implies a docker hub registry that requires login secrets)
// the name is restricted to 24 characters (7 from the resource group name, 1 hyphen, 13 from unique string, 1 hyphen, 2 from kv)
var finalKeyVaultName = generateKeyVaultName ? '${take(learningLoopRg.name, 7)}-${uniqueString(learningLoopRg.id)}-kv' : useKeyVault ? config.image.properties.credentials!.keyVault!.keyVaultName : null

module keyVault './modules/keyvault.bicep' = if (useKeyVault && config.image.properties.credentials!.keyVault!.createKeyVault) {
  name: 'keyVault'
  scope: learningLoopRg
  params: {
    keyVaultName: finalKeyVaultName!
    location: deployment().location
    userPrincipalId: userRoleAssignmentPrincipalId
    managedIdentityPrincipalId: managedIdentity.outputs.principalId
    kvImageRegistryUsernameId: config.image.properties.credentials!.keyVault!.kvUserNameId
    kvImageRegistryUsername: imageRegistryUsername ?? config.image.properties.credentials!.keyVault!.kvUsername
    kvImageRegistryPasswordId: config.image.properties.credentials!.keyVault!.kvPasswordId
    kvImageRegistryPassword: imageRegistryPassword ?? config.image.properties.credentials!.keyVault!.kvPassword
  }
}

// setup metrics if needed
var useMetrics = config.metrics != null
var generateAppInsightsName = useMetrics && config.metrics!.createApplicationInsights
var appInsightsName = useMetrics && config.metrics!.applicationInsightsName != null ?  config.metrics!.applicationInsightsName! : learningLoopRg.name

module appInsights './modules/appinsights.bicep' = if (useMetrics) {
  name: 'appInsights'
  scope: learningLoopRg
  params: {
    create: config.metrics!.createApplicationInsights
    generateName: generateAppInsightsName
    insightsName: appInsightsName
    location: deployment().location
  }
}

// generate the deployment parameters for the loop deployment
module appDeploymentParams './modules/generatedeploymentparams.bicep' = {
  name: 'appDeploymentParams'
  scope: learningLoopRg
  params: {
    config: config
    finalUserRoleAssignmentPrincipalId: userRoleAssignmentPrincipalId ?? ''
    finalKeyVaultName: finalKeyVaultName ?? ''
    finalAcrName: useAcr ? containerRegistry.outputs.acrName : ''
    finalManagedIdentityName: managedIdentity.outputs.managedIdentityName
    appInsightsConnectionString: appInsights.outputs.applicationInsightsConnectionString ?? ''
  }
}

output loopName string = config.loopConfig.name
output resourceGroupName string = learningLoopRg.name
output keyVaultName string = finalKeyVaultName ?? ''
output acrName string = useAcr ? containerRegistry.outputs.acrName : ''
output managedIdentityName string = managedIdentity.outputs.managedIdentityName
output appInsightsConnectionString string = useMetrics ? appInsights.outputs.applicationInsightsConnectionString : ''
output appInsightsInstrumentationKey string = useMetrics ? appInsights.outputs.applicationInsightsInstrumentationKey : ''
output loopDeploymentParams string = appDeploymentParams.outputs.loopDeploymentParams
output imageHost string = appDeploymentParams.outputs.imageHost
output imageName string = appDeploymentParams.outputs.imageName
output imageTag string = appDeploymentParams.outputs.imageTag
