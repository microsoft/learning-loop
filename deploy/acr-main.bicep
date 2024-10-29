import * as f from './modules/functions.bicep'
// This is the main Bicep file that deploys the container group, storage account, and event hub for the application.
// An Azure Container Registry (ACR) is used to managed the container image.
// The output generates the configuration for the reinforcement learning simulator.
@description('Location for all resources')
param location string = resourceGroup().location

@description('Learning Loop name')
param loopName string = 'sample-loop'
@description('Loop Container Group name')
param loopContainerGroupName string = 'sample-loop-cg'
@description('Container CPU Cores')
param containerCpuCores int = 4
@description('Container Memory in GB')
param containerMemoryGig int = 16
@description('Managed Identity name')
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
param containerImageName string = 'learning-loop'
@description('Container Image tag')
param containerImageTag string = 'latest'
@description('Registry Host')
param registryHost string = 'acrsampleloop${uniqueString(tenant().tenantId)}.azurecr.io'
@description('Principal ID for the role assignments')
param userRoleAssignmentPrincipalId string = ''
@description('Enable Application Insights')
param appInsightsEnabled bool = false
@description('Application Insights Connection String')
param appInsightsConnectionString string = ''
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
    value: string(appInsightsEnabled)
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING' 
    value: appInsightsConnectionString
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

var containerImage = {
  name: containerImageName
  tag: containerImageTag
  registry: {
    host: registryHost
    credentials: {
      isManagedIdentity: true
      username: managedIdentityName
      password: null
    }
  }
} 

module storage 'modules/storage.bicep' = {
  name: 'storage'
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

// Deploy the container group with a loop container instance
module loopContainerGroup 'modules/containergroup.bicep' = {
  name: 'loopContainer'
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
  params: {
    assignedRolePrincipalId: loopContainerGroup.outputs.containerPrincipalId
    storageAccountName: storage.outputs.storageAccountName
    eventHubsName: eventhubs.outputs.eventHubsName
  }
}

module rlSimConfig 'modules/generaterlsimconfig.bicep' = {
  name: 'rlSimConfig'
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
  params: {
    assignedRolePrincipalId: rlsimContainerGroup.outputs.containerPrincipalId
    storageAccountName: storage.outputs.storageAccountName
    eventHubsName: eventhubs.outputs.eventHubsName
  }
}

module userRollassignments 'modules/userrollassignments.bicep' = if (!empty(userRoleAssignmentPrincipalId)) {
  name: 'userRollassignments'
  params: {
    userRoleAssignmentPrincipalId: userRoleAssignmentPrincipalId
    storageAccountName: storage.outputs.storageAccountName
    eventHubsName: eventhubs.outputs.eventHubsName
  }
}

output rlSimConfigAz string = rlSimConfig.outputs.rlSimConfigAz
output rlSimConfigConnStr string = rlSimConfig.outputs.rlSimConfigConnStr
