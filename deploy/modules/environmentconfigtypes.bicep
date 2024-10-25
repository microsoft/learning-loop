@export()
type imageRepositoryKind = 'acr' | 'dockerhub'

@export()
type environmentConfigT = {
  @description('The name of the resource group in which the resources will be deployed.')
  resourceGroupName: string

  @description('Indicates if the managed identity should be created or used from an existing one.')
  createManagedIdentity: bool
  @description('Indicates if the managed identity name should be generated or use the supplied name.')
  generateManagedIdentityName: bool
  @description('The name of the managed identity to use; if generateManagedIdentityName is true, the name will be altered to be unique within the resource group.')
  managedIdentityName: string
  @description('The deployment users principal id to be used for the user role assignment.')
  userRoleAssignmentPrincipalId: string?

  @description('Parameters used to generate the application deployment bicep parameters for Leaning Loop deployment.')
  loopConfig: {
    @description('The application id (appId) of the Learning Loop.')
    name: string
    @description('Application environment variables from TrainerConfig, JoinerConfig, LogRetentionConfig, and TrainingMonitoringConfig')
    environmentVars: object[]?

    @description('True if the reinforcement learning simulator should be deployed.')
    deployRlSim: bool
    @description('Additional arguments for the reinforcement learning simulator')
    rlSimArgs: string

    @description('Event hub parameters')
    eventHub: {
      capacity: int
      partitionCount: int
      messageRetentionDays: int
    }
    @description('Container parameters')
    container: {
      cpuCores: int
      memoryGig: int
    }
  }

  @description('Learning Loop image repository configuration')
  image: {
    @description('Docker image name as stored in the image repository.')
    name: string
    @description('Docker image tag')
    tag: string
    @description('Image repository properties')
    properties: {
      @description('Kind of image repository to use (either acr or dockerhub).')
      kind: imageRepositoryKind
      @description('Indicates if the acr should be created or used from an existing one. Effective only if kind is acr.')
      createAcr: bool
      @description('The name of image repository to use. Effective only if kind is acr.')
      repositoryName: string
      @description('Indicates if the image repository name should be generated or use the supplied name.  Effective only if kind is acr.')
      generateRepoName: bool
      @description('The credentials to use to access the image repository. Effective only if kind is dockerhub.')
      credentials: {
        @description('The key vault details used to access the image repository.')
        keyVault: {
          @description('The key vault name to use to access the image repository.')
          keyVaultName: string?
          @description('Indicates if the key vault should be created or used from an existing one.')
          createKeyVault: bool
          @description('The subscription id of the key vault to use to access the image repository.')
          kvSubscriptionId: string
          @description('The key vault secret id for the user name to access a dockerhub image repository.')
          kvUserNameId: string
          @description('The user name to store in the key vault.')
          @secure()
          kvUsername: string
          @description('The key vault secret id for the password to access a dockerhub image repository.')
          kvPasswordId: string
          @description('The password secret to store in the key vault.')
          @secure()
          kvPassword: string
        }?
      }?
    }
  }
  @description('Application insights parameters')
  metrics: {
    @description('The name of the application insights instance to use to store metrics.')
    applicationInsightsName: string?
    @description('True to create a new application insights instance, false to use an existing one.')
    createApplicationInsights: bool
  }?
}
