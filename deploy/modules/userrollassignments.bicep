param storageAccountName string
param eventHubsName string
param userRoleAssignmentPrincipalId string

//
// Storage Account Role Assignments
//

resource storage 'Microsoft.Storage/storageAccounts@2022-09-01' existing = {
  name: storageAccountName
}

// Add role assignment to storage account for user access
resource storageUserRoleAssigment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(subscription().subscriptionId, 'UserStorageBlobDataContributor', storageAccountName, userRoleAssignmentPrincipalId)
  scope: storage
  properties: {
    principalId: userRoleAssignmentPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  }
}

//
// Event Hub Role Assignments
//

resource eventHubs 'Microsoft.EventHub/namespaces@2022-10-01-preview' existing = {
  name: eventHubsName
}

// Add role assignment to event hub receiver to the user object id
resource eventHubUserReceiverRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(subscription().subscriptionId, 'AzureUserEventHubDataReceiver', eventHubsName, userRoleAssignmentPrincipalId)
  scope: eventHubs
  properties: {
    principalId: userRoleAssignmentPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a638d3c7-ab3a-418d-83e6-5f17a39d4fde')
  }
}

// Add role assignment to event hub sender to the user object id
resource eventHubUserSenderRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(subscription().subscriptionId, 'AzureUserEventHubDataSender', eventHubsName, userRoleAssignmentPrincipalId)
  scope: eventHubs
  properties: {
    principalId: userRoleAssignmentPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '2b629674-e913-4c01-ae53-ef4638d8f975')
  }
}
