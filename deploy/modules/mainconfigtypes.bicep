import { storageSkuType } from './storage.bicep'
import { storageKindType } from './storage.bicep'
import { eventhubSkuType } from './eventhubs.bicep'

@export()
type credentialType = 'managedIdentity' | 'usernamePassword' | 'keyVault'

@export()
type mainConfigT = {
  @description('Application name defining a namespace for messaging and storage')
  appName: string
  @description('Application environment variables from TrainerConfig, JoinerConfig, LogRetentionConfig, and TrainingMonitoringConfig')
  environmentVars: object[]?
  @description('The name of the user-assigned managed identity for the tester (used for test ci/cd testing)')
  testerIdentityName: string?
  @description('Tags applied to each deployed resource')
  resourceTags: object?
  @description('Configuration for the storage account')  
  storage: {
    @description('The SKU of the storage account (e.g. Standard_LRS)')
    sku: storageSkuType
    @description('The kind of storage account (e.g. StorageV2)')
    kind: storageKindType
  }
  @description('Configuration for the event hub')
  eventhub: {
    @description('The SKU of the event hub (e.g. Standard)')
    sku: eventhubSkuType
    @description('The capacity of the event hub')
    @minValue(0)
    capacity: int
    @description('Number of days to retain the events for this Event Hub, value should be 1 to 7 days')
    @minValue(1)
    @maxValue(7)
    messageRetentionDays: int
    @description('The number of partitions in the Event Hub')
    @minValue(1)
    @maxValue(32)
    partitionCount: int
  }
  @description('Configuration for the container group hosting the application container')
  container: {
    @description('The number of CPUs requested for the container instance')
    cpuCores: int
    @description('The amount of memory in GB requested for the container instance')
    memoryGig: int
    @description('The image configuration for the container instance')
    image: {
      @description('The name of the container image')
      name: string
      @description('The tag of the container image')
      tag: string
      @description('The registry configuration for the container image')
      registry: {
        @description('The host of the container registry (e.g. myregistry.azurecr.io)')
        host: string
        @description('The credentials for the container registry')
        credentials: {
          @description('The container registry credential type')
          type: credentialType
          @description('The username for the container registry if using usernamePassword, the managed identity if using managedIdentity, or the key vault if using a key vault.')
          @secure()
          username: string?
          @description('The password for the container registry or null if using a key vault or a mangaged identity')
          @secure()
          password: string?
        }
      }
    }
  }?
}
