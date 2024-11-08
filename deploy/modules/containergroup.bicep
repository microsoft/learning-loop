// Deploys a container group for Leaning Loop; the Leaning Loop configuration
// can be supplied via the containerConfig parameters.
import * as f from './functions.bicep'

// container group setup
type containerConfigT = {
  @description('Name of the container group')
  name: string
  @description('Tags applied to each deployed resource')
  resourceTags: object?
  @description('Location for the container group')
  location: string
  @description('Environment variables for the container instance')
  environmentVars: object[]
  @description('Number of CPU cores to allocate for the container')
  cpuCores: int
  @description('Amount of memory in GB to allocate for the container')
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
        @description('Indicates if the container registry credentials are managed identity or username/password')
        isManagedIdentity: bool
        @description('The username for the container registry if not using managed identity; otherwise, the managed identity name')
        @secure()
        username: string
        @description('The password for the container registry')
        @secure()
        password: string?
      }
    }
  }
}

param containerConfig containerConfigT

module containerGroupMI 'containergroupmi.bicep' = if (containerConfig.image.registry.credentials.isManagedIdentity) {
  name: '${containerConfig.name}-mi'
  params: {
    containerConfig: containerConfig
  }
}

module containerGroupNonMI 'containergroupnonmi.bicep' = if (!containerConfig.image.registry.credentials.isManagedIdentity) {
  name: '${containerConfig.name}-nonmi'
  params: {
    containerConfig: containerConfig
  }
}

// output the principal ID for the container group so that it can be used for role assignments
output containerPrincipalId string = containerConfig.image.registry.credentials.isManagedIdentity ? containerGroupMI.outputs.containerPrincipalId : containerGroupNonMI.outputs.containerPrincipalId
