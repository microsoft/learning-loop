using 'environment.bicep'

param config = {
  resourceGroupName: 'rg-sample-loop'

  createManagedIdentity: true
  generateManagedIdentityName: true
  managedIdentityName: 'sample-loop'
  userRoleAssignmentPrincipalId: null

  loopConfig: {
    name: 'sample-loop'
    environmentVars: [
      {
        name: 'ExperimentalUnitDuration'
        value: '0:0:10'
      }
      {
        name: 'TrainerEnabled'
        value: true
      }
      {
        name: 'JoinerEnabled'
        value: true
      }
      {
        name: 'MachineLearningArguments'
        value: '--cb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type ips -q ::'
      }
      {
        name: 'LastConfigurationEditDate'
        value: '2024-01-01'
      }
    ]
    deployRlSim: true
    rlSimArgs: ''
    eventHub: {
      capacity: 4
      partitionCount: 16
      messageRetentionDays: 1
    }
    container: {
      cpuCores: 4
      memoryGig: 16
    }
  }

  image: {
    name: 'learningloop/learning-loop'
    tag: 'latest'
    properties: {
      kind: 'dockerhub'
      createAcr: false
      repositoryName: ''
      generateRepoName: false
      credentials: {
        keyVault: {
          keyVaultName: null
          createKeyVault: true
          kvSubscriptionId: ''
          kvUserNameId: 'repo-user-name-id'
          kvUsername: ''
          kvPasswordId: 'repo-password-id'
          kvPassword: ''
        }
      }
    }
  }
  metrics: {
    applicationInsightsName: null
    createApplicationInsights: true
  }
}
