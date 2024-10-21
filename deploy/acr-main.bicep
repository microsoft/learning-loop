import * as f from './modules/functions.bicep'
// This is the main Bicep file that deploys the container group, storage account, and event hub for the application.
// An Azure Container Registry (ACR) is used to managed the container image.
// The output generates the configuration for the reinforcement learning simulator.
@description('Location for all resources')
param location string = resourceGroup().location

@description('Learning Loop name')
param loopName string = 'sample-loop'
@description('Container Group name')
param containerGroupName string = 'sample-loop-cg'
@description('Container CPU Cores')
param containerCpuCores int = 4
@description('Container Memory in GB')
param containerMemoryGig int = 16
@description('Managed Identity name')
param managedIdentityName string = 'mi-sample-loop-${uniqueString(tenant().tenantId)}'
@description('Storage Account name')
param storageAccountName string = 'sampleloopstg'
@description('Storage Account SKU')
param storageAccountSku string = 'Standard_LRS'
@description('Event Hubs name')
param eventhubsName string = 'sample-loop-eh'
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

// Generate the default environment variables and combine with the main configuration environment variables
var containerEnvironmentVars = [
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

// Deploy the container group, storage account, and event hub
module containerGroup 'modules/containergroup.bicep' = {
  name: 'container'
  params: {
    containerConfig: {
      name: containerGroupName
      resourceTags: resourceTags
      location: location
      environmentVars: containerEnvironmentVars
      cpuCores: containerCpuCores
      memoryGig: containerMemoryGig
      image: containerImage
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
      roleAssignmentPrincipalId: containerGroup.outputs.containerPrincipalId
      storageUserObjectId: userRoleAssignmentPrincipalId
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
      roleAssignmentPrincipalId: containerGroup.outputs.containerPrincipalId
      senderReceiverUserObjectId: userRoleAssignmentPrincipalId
    }
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

output rlSimConfigAz string = rlSimConfig.outputs.rlSimConfigAz
output rlSimConfigConnStr string = rlSimConfig.outputs.rlSimConfigConnStr
