// Generate the Learning Loop deployment parameters based on the environment setup
import * as envTypes from './environmentconfigtypes.bicep'

param config envTypes.environmentConfigT
param finalUserRoleAssignmentPrincipalId string
param finalKeyVaultName string
param finalAcrName string
param finalManagedIdentityName string
param appInsightsConnectionString string

// build the environment variables for the app container
var environmentVars = !empty(config.loopConfig.environmentVars) ? config.loopConfig.environmentVars : []
var environmentVarsStr = join(map(environmentVars!, e => format('\t\t{{\n\t\t\tname: \'{0}\'\n\t\t\tvalue: \'{1}\'\n\t\t}}', e.name, e.value)) , '\n')

// generate key vault subscription id if needed
var kvSubscriptionId = (config.image.properties.kind == 'dockerhub') ? config.image.properties.credentials!.keyVault!.kvSubscriptionId : ''
var finalKvSubscriptionId = (config.image.properties.kind == 'dockerhub') && empty( kvSubscriptionId) ? subscription().subscriptionId : kvSubscriptionId

// generate key vault secrets if needed
var keyVaultSecretsVar_0 = config.image.properties.kind == 'acr' ? '' : replace('''
param kvImageRegistryUsername = getSecret('{subscriptionid}', '{resourcegroup}', '{keyvaultname}', '{usersecretid}')
param kvImageRegistryPassword = getSecret('{subscriptionid}', '{resourcegroup}', '{keyvaultname}', '{passwordsecretid}')
''', '{subscriptionid}', finalKvSubscriptionId)

var keyVaultSecretsVar_1 = config.image.properties.kind == 'acr' ? '' : replace(keyVaultSecretsVar_0, '{resourcegroup}', config.resourceGroupName)
var keyVaultSecretsVar_2 = config.image.properties.kind == 'acr' ? '' : replace(keyVaultSecretsVar_1, '{keyvaultname}', finalKeyVaultName)
var keyVaultSecretsVar_3 = config.image.properties.kind == 'acr' ? '' : replace(keyVaultSecretsVar_2, '{usersecretid}', config.image.properties.credentials!.keyVault!.kvUserNameId)
var keyVaultSecretsVarFinal = config.image.properties.kind == 'acr' ? '' : replace(keyVaultSecretsVar_3, '{passwordsecretid}', config.image.properties.credentials!.keyVault!.kvPasswordId)
var keyVaultSecretsVar = keyVaultSecretsVarFinal

// generate user role assignment principal id  if needed
var roleAssignmentUserObjectIdVarFinal = empty(finalUserRoleAssignmentPrincipalId) ? '''
roleAssignmentUserObjectId: null
''' : replace('''
roleAssignmentUserObjectId: '{0}'
''', '{0}', finalUserRoleAssignmentPrincipalId)
var roleAssignmentUserObjectIdVar = roleAssignmentUserObjectIdVarFinal

// generate app insights setup if needed
var appInsightsEnvVarFinal = empty(appInsightsConnectionString) ? '' : replace('''
      {
        name: 'AzureMonitorMetricExporterEnabled' 
        value: 'true'
      }
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING' 
        value: '{0}'
      }
''', '{0}', appInsightsConnectionString)
var appInsightsEnvVar = appInsightsEnvVarFinal

// generate image host and credentials if needed
var imageHostVar = config.image.properties.kind == 'acr' ? '${finalAcrName}.azurecr.io' : 'docker.io'

var imageCredsVarFinal = config.image.properties.kind == 'acr' ? replace('''
          credentials: {
            type: 'managedIdentity'
            username: '{0}'
          }
''', '{0}', finalManagedIdentityName) : '''
          credentials: {
            type: 'keyVault'
          }
'''
var imageCredsVar = imageCredsVarFinal

// generate the deployment parameters using replacement values
var appDeploymentParams_0 = replace('''
//
// bicep pameters file generated by 'deploy-sample.ps1'
//
using 'main.bicep'
{kvsecretvars}
param mainConfig = {
   appName: '{loopname}'
   {roleAssignmentUserObjectId}
   environmentVars: [
{appInsightsEnv}
{environmentVarsString}
   ]
   resourceTags: {
      deploymentGroupName: '{loopname}'
   }
   storage: {
      sku: 'Standard_LRS'
      kind: 'StorageV2'
   }
   eventhub: {
      capacity: {eventhub-capacity}
      partitionCount: {eventhub-partitioncount}
      sku: 'Standard'
      messageRetentionDays: {eventhub-messageretentiondays}
   }
   container: {
      cpuCores: {container-cpucores}
      memoryGig: {container-memorygig}
      image: {
        registry: {
          host: '{imageHost}'
          {imageCreds}
        }
        name: '{image-name}'
        tag: '{image-tag}'
      }
   }
}
''', '{kvsecretvars}', keyVaultSecretsVar)

var appDeploymentParams_1 = replace(appDeploymentParams_0, '{roleAssignmentUserObjectId}', roleAssignmentUserObjectIdVar)
var appDeploymentParams_2 = replace(appDeploymentParams_1, '{appInsightsEnv}', appInsightsEnvVar)
var appDeploymentParams_3 = replace(appDeploymentParams_2, '{imageHost}', imageHostVar)
var appDeploymentParams_4 = replace(appDeploymentParams_3, '{imageCreds}', imageCredsVar)
var appDeploymentParams_5 = replace(appDeploymentParams_4, '{loopname}', config.loopConfig.name)
var appDeploymentParams_6 = replace(appDeploymentParams_5, '{eventhub-capacity}', string(config.loopConfig.eventHub.capacity))
var appDeploymentParams_7 = replace(appDeploymentParams_6, '{eventhub-partitioncount}', string(config.loopConfig.eventHub.partitionCount))
var appDeploymentParams_8 = replace(appDeploymentParams_7, '{eventhub-messageretentiondays}', string(config.loopConfig.eventHub.messageRetentionDays))
var appDeploymentParams_9 = replace(appDeploymentParams_8, '{container-cpucores}', string(config.loopConfig.container.cpuCores))
var appDeploymentParams_10 = replace(appDeploymentParams_9, '{container-memorygig}', string(config.loopConfig.container.memoryGig))
var appDeploymentParams_11 = replace(appDeploymentParams_10, '{image-name}', config.image.name)
var appDeploymentParams_12 = replace(appDeploymentParams_11, '{image-tag}', config.image.tag)
var appDeploymentParamsFinal = replace(appDeploymentParams_12, '{environmentVarsString}', environmentVarsStr)
var appDeploymentParams = appDeploymentParamsFinal

output loopDeploymentParams string = appDeploymentParams
output imageHost string = imageHostVar
output imageName string = config.image.name
output imageTag string = config.image.tag
