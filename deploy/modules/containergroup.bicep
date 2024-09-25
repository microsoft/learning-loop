import * as f from './functions.bicep'

// container group setup
type containerConfigT = {
  @description('Name of the container group')
  name: string
  @description('Tags applied to each deployed resource')
  resourceTags: object?
  @description('Location for the container group')
  location: string
  @description('Environment variables for the container application')
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

// construct the container image path
var containerImagePath = f.makeContainerImagePath(containerConfig.image.registry.host, containerConfig.image.name, containerConfig.image.tag)

// get the identity for the container group if using managed identity for the registry credentials
resource acrPullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = if (containerConfig.image.registry.credentials.isManagedIdentity) {
  name: containerConfig.image.registry.credentials.username
}

// setup the identity type and user assigned identities for the container group
var containerGroupIdentityType = containerConfig.image.registry.credentials.isManagedIdentity ? 'SystemAssigned, UserAssigned' : 'SystemAssigned'
var userAssignedIdentities = containerConfig.image.registry.credentials.isManagedIdentity ? { '${acrPullIdentity.id}': {} } : null

// setup the image registry credentials
var imageRegistryCredentials = containerConfig.image.registry.credentials.isManagedIdentity ? [{
    server: containerConfig.image.registry.host
    identity: acrPullIdentity.id
  }] : [{
    server: containerConfig.image.registry.host
    username: containerConfig.image.registry.credentials.username
    password: containerConfig.image.registry.credentials.password
  }]

// create the container group
resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2021-09-01' = {
  name: containerConfig.name
  location: containerConfig.location
  tags: containerConfig.resourceTags
  identity: {
    type: containerGroupIdentityType
    userAssignedIdentities: userAssignedIdentities
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
    imageRegistryCredentials: imageRegistryCredentials
  }
}

// output the principal ID for the container group so that it can be used for role assignments
output containerPrincipalId string = containerGroup.identity.principalId
