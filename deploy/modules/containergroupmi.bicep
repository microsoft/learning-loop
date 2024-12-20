// Note: this bicep script is for container group using managed identity to pull image from ACR
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
        @description('The managed identity name')
        @secure()
        username: string
        // NOTE: password is not used in this template; it is here to provide a consistent experience with the non-MI version
        //       reducing the need for conditional logic in the calling template
        @description('The password for the container registry')
        @secure()
        password: string?
      }
    }
  }
}

param containerConfig containerConfigT

// construct the container image path
var containerImagePath = f.makeContainerImagePath(containerConfig.image.registry.host, containerConfig.image.name, containerConfig.image.tag)

// get the identity for the container group if using managed identity for the registry credentials
resource acrPullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: containerConfig.image.registry.credentials.username
}

// create the container group
resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2021-09-01' = {
  name: containerConfig.name
  location: containerConfig.location
  tags: containerConfig.resourceTags
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: { '${acrPullIdentity.id}': {} }
  }
  properties: {
    containers: [
      {
        name: containerConfig.name
        properties: {
          image: containerImagePath
          environmentVariables: containerConfig.environmentVars
          resources: {
            requests: {
              cpu: containerConfig.cpuCores
              memoryInGB: containerConfig.memoryGig
            }
          }
        }
      }
    ]
    osType: 'Linux'
    restartPolicy: 'OnFailure'
    imageRegistryCredentials: [
      {
        server: containerConfig.image.registry.host
        identity: acrPullIdentity.id
      }
    ]  
  }
}

// output the principal ID for the container group so that it can be used for role assignments
output containerPrincipalId string = containerGroup.identity.principalId
