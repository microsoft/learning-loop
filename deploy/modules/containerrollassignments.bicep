param storageAccountName string
param eventHubsName string
param assignedRolePrincipalId string

//
// Storage Account Role Assignments
//

resource storage 'Microsoft.Storage/storageAccounts@2022-09-01' existing = {
  name: storageAccountName
}

// Add role assignment to storage account for Storage Blob Data Contributor
resource storageRoleAssigment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(subscription().subscriptionId, 'StorageBlobDataContributor', storageAccountName, assignedRolePrincipalId)
  scope: storage
  properties: {
    principalId: assignedRolePrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  }
}

//
// Event Hub Role Assignments
//

resource eventHubs 'Microsoft.EventHub/namespaces@2022-10-01-preview' existing = {
  name: eventHubsName
}

// Add role assignment to event hub receiver
resource eventHubRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(subscription().subscriptionId, 'AzureEventHubDataReceiver', eventHubsName, assignedRolePrincipalId)
  scope: eventHubs
  properties: {
    principalId: assignedRolePrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a638d3c7-ab3a-418d-83e6-5f17a39d4fde')
  }
}

// Add role assignment to event hub sender
resource eventHubUserSenderRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(subscription().subscriptionId, 'AzureEventHubDataSender', eventHubsName, assignedRolePrincipalId)
  scope: eventHubs
  properties: {
    principalId: assignedRolePrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '2b629674-e913-4c01-ae53-ef4638d8f975')
  }
}
