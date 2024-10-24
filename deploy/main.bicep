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

module formatLoopContainerName 'modules/formatcontainername.bicep' = {
  name: 'formatLoopContainerNameModule'
  params: {
    containerName: f.makeAppContainerGroupName(mainConfig.appName)
  }
}

module formatRlSimContainerName 'modules/formatcontainername.bicep' = {
  name: 'formatRlSimContainerNameModule'
  params: {
    containerName: f.makeAppContainerGroupName('${mainConfig.appName}-rlsim')
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
var loopContainerEnvironmentVars = concat(defaultEnvironmentVars, mainConfig.environmentVars ?? [])

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

// Deploy the container group, storage account, and event hub
module loopContainerGroup 'modules/containergroup.bicep' = {
  name: 'loopContainer'
  params: {
    containerConfig: {
      name: formatLoopContainerName.outputs.formattedContainerName
      resourceTags: mainConfig.resourceTags
      location: location
      environmentVars: loopContainerEnvironmentVars
      cpuCores: mainConfig.container!.cpuCores
      memoryGig: mainConfig.container!.memoryGig
      image: containerImage
    }
  }
}

module loopRollassignments 'modules/containerrollassignments.bicep' = {
  name: 'loopRollassignments'
  params: {
    assignedRolePrincipalId: loopContainerGroup.outputs.containerPrincipalId
    storageAccountName: storage.outputs.storageAccountName
    eventHubsName: eventhubs.outputs.eventHubsName
  }
}

// rl_sim container instance environment variables
var rlsimContainerEnvironmentVars = mainConfig.deployRlSimContainer ? [
  {
    name: 'RL_START_WITH'
    value: 'rl_sim.sh'
  }
  {
    name: 'RL_SIM_CONFIG'
    value: rlSimConfig.outputs.rlSimConfigAz
  }
] : []

// Deploy the rl_sim container group
module rlsimContainerGroup 'modules/containergroup.bicep' = if (mainConfig.deployRlSimContainer) {
  name: 'rlsimContainer'
  params: {
    containerConfig: {
      name: formatRlSimContainerName.outputs.formattedContainerName
      resourceTags: mainConfig.resourceTags
      location: location
      environmentVars: rlsimContainerEnvironmentVars
      cpuCores: mainConfig.container!.cpuCores
      memoryGig: mainConfig.container!.memoryGig
      image: containerImage
    }
  }
}

module rlSimRollassignments 'modules/containerrollassignments.bicep' = if (mainConfig.deployRlSimContainer) {
  name: 'rlSimRollassignments'
  params: {
    assignedRolePrincipalId: rlsimContainerGroup.outputs.containerPrincipalId
    storageAccountName: storage.outputs.storageAccountName
    eventHubsName: eventhubs.outputs.eventHubsName
  }
}

module userRollassignments 'modules/userrollassignments.bicep' = if (!empty(mainConfig.roleAssignmentUserObjectId)) {
  name: 'userRollassignments'
  params: {
    userRoleAssignmentPrincipalId: mainConfig.roleAssignmentUserObjectId!
    storageAccountName: storage.outputs.storageAccountName
    eventHubsName: eventhubs.outputs.eventHubsName
  }
}

output rlSimContainerDeployed bool = mainConfig.deployRlSimContainer
output eventHubEndpoint string = eventhubs.outputs.eventHubEndpoint
output storageBlobEndpoint string = storage.outputs.storageBlobEndpoint
output storageAccountName string = storage.outputs.storageAccountName
output rlSimConfigAz string = rlSimConfig.outputs.rlSimConfigAz
output rlSimConfigConnStr string = rlSimConfig.outputs.rlSimConfigConnStr
