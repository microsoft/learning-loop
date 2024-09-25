// This is the main Bicep file that deploys the container group, storage account, and event hub for the application.
import * as conf from './modules/mainconfigtypes.bicep'
import * as f from './modules/functions.bicep'

// START: deploy/main.bicep
@description('Location for all resources')
param location string = resourceGroup().location

@description('Command line override of mainConfig.container.image.tag')
param containerImageTag string?

@description('Key Vault username for the image registry when the credentials type is keyVault')
@secure()
param kvImageRegistryUsername string?
@description('Key Vault password for the image registry when the credentials type is keyVault')
@secure()
param kvImageRegistryPassword string?

@description('Main configuration for the deployment')
param mainConfig conf.mainConfigT

// Generate the storage account name, event hub name, and container group name
var storageAccountName = f.makeStorageAccountName(mainConfig.appName)
var eventHubName = f.makeEventHubName(mainConfig.appName)
var containerGroupName = f.makeAppContainerGroupName(mainConfig.appName)

// Generate the default environment variables and combine with the main configuration environment variables
var defaultEnvironmentVars = [
  {
    name: 'AppId'
    value: mainConfig.appName
  }
  {
    name: 'StorageAccountUrl'
    value: f.makeStorageAccountUrl(storageAccountName)
  }
  {
    name: 'FullyQualifiedEventHubNamespace'
    value: f.makeEventhubNamespace(eventHubName)
  }
]
var containerEnvironmentVars = concat(defaultEnvironmentVars, mainConfig.environmentVars ?? [])

var containerImage = {
  name: mainConfig.container!.image.name
  tag: containerImageTag ?? mainConfig.container!.image.tag
  registry: {
    host: mainConfig.container!.image.registry.host
    credentials: (mainConfig.container!.image.registry.credentials.type == 'managedIdentity') ? {
      isManagedIdentity: true
      username: mainConfig.container!.image.registry.credentials.username
      password: null
    } : (mainConfig.container!.image.registry.credentials.type == 'usernamePassword') ? {
      isManagedIdentity: false
      username: mainConfig.container!.image.registry.credentials.username
      password: mainConfig.container!.image.registry.credentials.password
    } : (mainConfig.container!.image.registry.credentials.type == 'keyVault') ? {
      isManagedIdentity: false
      username: kvImageRegistryUsername
      password: kvImageRegistryPassword
    } : { }
  }
} 

// Deploy the container group, storage account, and event hub
module containerGroup 'modules/containergroup.bicep' = {
  name: 'container'
  params: {
    containerConfig: {
      name: containerGroupName
      resourceTags: mainConfig.resourceTags
      location: location
      environmentVars: containerEnvironmentVars
      cpuCores: mainConfig.container!.cpuCores
      memoryGig: mainConfig.container!.memoryGig
      image: containerImage
    }
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    storageConfig: {
      name: storageAccountName
      resourceTags: mainConfig.resourceTags
      location: location
      sku: mainConfig.storage.sku
      kind: mainConfig.storage.kind
      blobContainerName: mainConfig.appName
      roleAssignmentPrincipalId: containerGroup.outputs.containerPrincipalId
    }
  }
}

module eventhubs 'modules/eventhubs.bicep' = {
  name: 'eventhubs'
  params: {
    eventhubsConfig: {
      name: eventHubName
      resourceTags: mainConfig.resourceTags
      location: location
      sku: mainConfig.eventhub.sku
      capacity: mainConfig.eventhub.capacity
      messageRetentionDays: mainConfig.eventhub.messageRetentionDays
      partitionCount: mainConfig.eventhub.partitionCount
      roleAssignmentPrincipalId: containerGroup.outputs.containerPrincipalId
    }
  }
}
