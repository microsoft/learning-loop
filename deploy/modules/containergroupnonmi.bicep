// Note: this bicep script is for container group requiring username/password 
// or anonymous access to the container registry.
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
        @description('The username for the container registry (leave empty string for anonymous access)')
        @secure()
        username: string
        @description('The password for the container registry (set to null or empty for anonymous access)')
        @secure()
        password: string?
      }
    }
  }
}

param containerConfig containerConfigT

// construct the container image path
var containerImagePath = f.makeContainerImagePath(containerConfig.image.registry.host, containerConfig.image.name, containerConfig.image.tag)

// create the container group
resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2021-09-01' = {
  name: containerConfig.name
  location: containerConfig.location
  tags: containerConfig.resourceTags
  identity: {
    type: 'SystemAssigned'
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
    imageRegistryCredentials: !empty(containerConfig.image.registry.credentials.username) ? [
      {
        server: containerConfig.image.registry.host
        username: containerConfig.image.registry.credentials.username
        password: containerConfig.image.registry.credentials.password
      }
    ] : null
  }
}

// output the principal ID for the container group so that it can be used for role assignments
output containerPrincipalId string = containerGroup.identity.principalId
