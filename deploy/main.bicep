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

module formatContainerName 'modules/formatcontainername.bicep' = {
  name: 'formatContainerNameModule'
  params: {
    containerName: f.makeAppContainerGroupName(mainConfig.appName)
  }
}

module formatEventHubName 'modules/formateventhubname.bicep' = {
  name: 'formatEventHubNameModule'
  params: {
    eventhubName: f.makeEventHubName(mainConfig.appName)
  }
}

module formatStorageAccountName 'modules/formatstorageaccoutname.bicep' = {
  name: 'formatStorageAccountNameModule'
  params: {
    storageAccountName: f.makeStorageAccountName(mainConfig.appName)
  }
}

// Generate the default environment variables and combine with the main configuration environment variables
var defaultEnvironmentVars = [
  {
    name: 'AppId'
    value: mainConfig.appName
  }
  {
    name: 'StorageAccountUrl'
    value: f.makeStorageAccountUrl(formatStorageAccountName.outputs.formattedStorageAccountName)
  }
  {
    name: 'FullyQualifiedEventHubNamespace'
    value: f.makeEventhubNamespace(formatEventHubName.outputs.formattedEventHubName)
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
      name: formatContainerName.outputs.formattedContainerName
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
      name: formatStorageAccountName.outputs.formattedStorageAccountName
      resourceTags: mainConfig.resourceTags
      location: location
      sku: mainConfig.storage.sku
      kind: mainConfig.storage.kind
      blobContainerName: mainConfig.appName
      roleAssignmentPrincipalId: containerGroup.outputs.containerPrincipalId
      storageUserObjectId: mainConfig.roleAssignmentUserObjectId
    }
  }
}

module eventhubs 'modules/eventhubs.bicep' = {
  name: 'eventhubs'
  params: {
    eventhubsConfig: {
      name: formatEventHubName.outputs.formattedEventHubName
      resourceTags: mainConfig.resourceTags
      location: location
      sku: mainConfig.eventhub.sku
      capacity: mainConfig.eventhub.capacity
      messageRetentionDays: mainConfig.eventhub.messageRetentionDays
      partitionCount: mainConfig.eventhub.partitionCount
      roleAssignmentPrincipalId: containerGroup.outputs.containerPrincipalId
      senderReceiverUserObjectId: mainConfig.roleAssignmentUserObjectId
    }
  }
}

module rlSimConfig 'modules/generaterlsimconfig.bicep' = {
  name: 'rlSimConfig'
  params: {
    loopName: mainConfig.appName
    eventHubEndpoint: eventhubs.outputs.eventHubEndpoint
    storageBlobEndpoint: storage.outputs.storageBlobEndpoint
  }
}

output eventHubEndpoint string = eventhubs.outputs.eventHubEndpoint
output storageBlobEndpoint string = storage.outputs.storageBlobEndpoint
output storageAccountName string = storage.outputs.storageAccountName
output rlSimConfigAz string = rlSimConfig.outputs.rlSimConfigAz
output rlSimConfigConnStr string = rlSimConfig.outputs.rlSimConfigConnStr
