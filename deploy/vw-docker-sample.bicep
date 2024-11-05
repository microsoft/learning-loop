import * as f from './modules/functions.bicep'
// Template for creating the resources needed for a Learning Loop deployment using an Azure Container Registry.
// This includes the resource group, managed identity, container registry, and application insights resources.
// The output of this module include:
//    resourceGroupName - the resource group name
//    managedIdentityName - the name of the managed identity
//    appInsightsConnectionString - the connection string for the application insights resource (if app insights is used)
//    rlSimConfigAz - the reinforcement learning simulator configuration using Azure Credentials
//    rlSimConfigConnStr - the reinforcement learning simulator configuration using a connection string
// The outputs can be used to setup the Learning Loop application deployment using acr-main.bicep.
targetScope = 'subscription'

@description('The new resource group for the learning loop deployment.')
param resourceGroupName string = 'rg-sample-loop'

@description('Application Insights name.')
param appInsightsName string = 'ai-metrics-${uniqueString(tenant().tenantId, deployment().location)}'

@description('Learning Loop name')
param loopName string = 'sample-loop'
@description('Loop Container Group name')
param loopContainerGroupName string = 'sample-loop-cg'
@description('Container CPU Cores')
param containerCpuCores int = 4
@description('Container Memory in GB')
param containerMemoryGig int = 16
@description('The managed identity name for learning loop role assignments.')
param managedIdentityName string = 'mi-sample-loop-${uniqueString(tenant().tenantId)}'
@description('Storage Account name')
param storageAccountName string = 'stgsmpllp${uniqueString(tenant().tenantId)}'
@description('Storage Account SKU')
param storageAccountSku string = 'Standard_LRS'
@description('Event Hubs name')
param eventhubsName string = 'eh-sample-loop-${uniqueString(tenant().tenantId)}'
@description('Event Hubs capacity')
param eventhubsCapacity int = 4
@description('Event Hubs partition count')
param eventhubsPartitionCount int = 16
@description('Event Hubs retention days')
param eventhubsRetentionDays int = 1
@description('Container Image name')
param containerImageName string = 'vowpalwabbit/learning-loop'
@description('Container Image tag')
param containerImageTag string = 'latest'
@description('Registry Host')
param registryHost string = 'docker.io'
@description('Principal ID for the role assignments')
param userRoleAssignmentPrincipalId string = ''
@description('Enable Application Insights')
param appInsightsEnabled bool = true
@description('Loop Experimental Unit Duration')
param loopEnvVarExperimentalUnitDuration string = '0:0:10'
@description('Loop Trainer Enabled')
param loopEnvVarTrainerEnabled bool = true
@description('Loop Joiner Enabled')
param loopEnvVarJoinerEnabled bool = true
@description('Loop Machine Learning Arguments')
param loopEnvVarMachineLearningArguments string = '--cb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type ips -q ::'
@description('Loop Last Configuration Edit Date')
param loopEnvVarLastConfigurationEditDate string = '2024-01-01'
@description('Tag for all resources')
param resourceTags object = {
  deploymentGroupName: 'sample-loop'
}
@description('Deploy the reinforcement learning simulator')
param deployRlSim bool = true
@description('Additional arguments for the reinforcement learning simulator')
param rlSimArgs string = ''

var location = deployment().location

var containerImage = {
  name: containerImageName
  tag: containerImageTag
  registry: {
    host: registryHost
    credentials: {
      isManagedIdentity: false
      username: ''
      password: null
    }
  }
} 

// create the resource in the deployment location
resource learningLoopRg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resourceGroupName
  location: deployment().location
}

module managedIdentity './modules/managedidentity.bicep' = {
  name: 'managedIdentity'
  scope: learningLoopRg
  params: {
    name: managedIdentityName
    createManagedIdentity: true
    location: deployment().location
  }
}

// setup metrics if needed
module appInsights './modules/appinsights.bicep' = if (appInsightsEnabled) {
  name: 'appInsights'
  scope: learningLoopRg
  params: {
    create: true
    generateName: false
    insightsName: appInsightsName
    location: deployment().location
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  scope: learningLoopRg
  params: {
    storageConfig: {
      name: storageAccountName
      resourceTags: resourceTags
      location: location
      sku: storageAccountSku
      kind: 'StorageV2'
      blobContainerName: loopName
    }
  }
}

module eventhubs 'modules/eventhubs.bicep' = {
  name: 'eventhubs'
  scope: learningLoopRg
  params: {
    eventhubsConfig: {
      name: eventhubsName
      resourceTags: resourceTags
      location: location
      sku: 'Basic'
      capacity: eventhubsCapacity
      messageRetentionDays: eventhubsRetentionDays
      partitionCount: eventhubsPartitionCount
    }
  }
}

var appInsightsEnabledVal = !empty(appInsights)
var appInsightsConnectionStringVal = empty(appInsights) ? '' : appInsights.outputs.applicationInsightsConnectionString

// Generate the default environment variables and combine with the main configuration environment variables
var loopContainerEnvironmentVars = [
  {
    name: 'AppId'
    value: loopName
  }
  {
    name: 'StorageAccountUrl'
    value: f.makeStorageAccountUrl(storageAccountName)
  }
  {
    name: 'FullyQualifiedEventHubNamespace'
    value: f.makeEventhubNamespace(eventhubsName)
  }
  {
    name: 'AzureMonitorMetricExporterEnabled' 
    value: string(appInsightsEnabledVal)
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING' 
    value: appInsightsConnectionStringVal
  }
  {
    name: 'ExperimentalUnitDuration'
    value: loopEnvVarExperimentalUnitDuration
  }
  {
    name: 'TrainerEnabled'
    value: loopEnvVarTrainerEnabled
  }
  {
    name: 'JoinerEnabled'
    value: loopEnvVarJoinerEnabled
  }
  {
    name: 'MachineLearningArguments'
    value: loopEnvVarMachineLearningArguments
  }
  {
    name: 'LastConfigurationEditDate'
    value: loopEnvVarLastConfigurationEditDate
  }
]

// Deploy the container group with a loop container instance
module loopContainerGroup 'modules/containergroup.bicep' = {
  name: 'loopContainer'
  scope: learningLoopRg
  params: {
    containerConfig: {
      name: loopContainerGroupName
      resourceTags: resourceTags
      location: location
      environmentVars: loopContainerEnvironmentVars
      cpuCores: containerCpuCores
      memoryGig: containerMemoryGig
      image: containerImage
    }
  }
}

module loopRollassignments 'modules/containerrollassignments.bicep' = {
  name: 'loopRollAssignments'
  scope: learningLoopRg
  params: {
    assignedRolePrincipalId: loopContainerGroup.outputs.containerPrincipalId
    storageAccountName: storage.outputs.storageAccountName
    eventHubsName: eventhubs.outputs.eventHubsName
  }
}

module rlSimConfig 'modules/generaterlsimconfig.bicep' = {
  name: 'rlSimConfig'
  scope: learningLoopRg
  params: {
    loopName: loopName
    eventHubEndpoint: eventhubs.outputs.eventHubEndpoint
    storageBlobEndpoint: storage.outputs.storageBlobEndpoint
  }
}

// rl_sim container instance environment variables
var rlsimContainerEnvironmentVars = deployRlSim ? [
    {
      name: 'RL_START_WITH'
      value: 'rl_sim.sh'
    }
    {
      name: 'RL_SIM_CONFIG'
      value: rlSimConfig.outputs.rlSimConfigAz
    }
    {
      name: 'RL_SIM_ARGS'
      value: rlSimArgs
    }
    {
      name: 'LEARNING_LOOP_NAME'
      value: loopName
    }
    {
      name: 'STORAGE_ACCOUNT_NAME'
      value: storageAccountName
    }
  ] : []


// Deploy the rl_sim container group
module rlsimContainerGroup 'modules/containergroup.bicep' = if (deployRlSim) {
  name: 'rlsimContainer'
  scope: learningLoopRg
  params: {
    containerConfig: {
      name: 'rlsim-${loopContainerGroupName}'
      resourceTags: resourceTags
      location: location
      environmentVars: rlsimContainerEnvironmentVars
      cpuCores: containerCpuCores
      memoryGig: containerMemoryGig
      image: containerImage
    }
  }
}

module rlSimRollassignments 'modules/containerrollassignments.bicep' = if (deployRlSim) {
  name: 'rlSimRollassignments'
  scope: learningLoopRg
  params: {
    assignedRolePrincipalId: rlsimContainerGroup.outputs.containerPrincipalId
    storageAccountName: storage.outputs.storageAccountName
    eventHubsName: eventhubs.outputs.eventHubsName
  }
}

module userRollassignments 'modules/userrollassignments.bicep' = if (!empty(userRoleAssignmentPrincipalId)) {
  name: 'userRollassignments'
  scope: learningLoopRg
  params: {
    userRoleAssignmentPrincipalId: userRoleAssignmentPrincipalId
    storageAccountName: storage.outputs.storageAccountName
    eventHubsName: eventhubs.outputs.eventHubsName
  }
}

output resourceGroupName string = learningLoopRg.name
output managedIdentityName string = managedIdentity.outputs.managedIdentityName
output appInsightsConnectionString string = appInsights.outputs.applicationInsightsConnectionString
output rlSimConfigAz string = rlSimConfig.outputs.rlSimConfigAz
output rlSimConfigConnStr string = rlSimConfig.outputs.rlSimConfigConnStr
