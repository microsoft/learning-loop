// event hub setup
@export()
type eventhubSkuType = 'Basic' | 'Premium' | 'Standard'

type eventhubsConfigT = {
  @description('Name of the event hub namespace')
  name: string
  @description('Tags applied to each deployed resource')
  resourceTags: object?
  @description('Location for the event hub namespace')
  location: string
  @description('SKU for the event hub namespace (e.g. Standard)')
  sku: eventhubSkuType
  @description('The capacity of the event hub')
  @minValue(0)
  @maxValue(20)
  capacity: int
  @description('Number of days to retain the events for this Event Hub, value should be 1 to 7 days')
  @minValue(1)
  @maxValue(7)
  messageRetentionDays: int
  @description('The number of partitions in the Event Hub')
  @minValue(1)
  @maxValue(32)
  partitionCount: int
  @description('Principal ID for the role assignments')
  roleAssignmentPrincipalId: string
}

param eventhubsConfig eventhubsConfigT

// local constants
var observationEventHubName = 'observation'
var interactionEventHubName = 'interaction'
var minTlsVersion = '1.2'

// create the event hub namespace
resource eventHub 'Microsoft.EventHub/namespaces@2022-10-01-preview' = {
  name: eventhubsConfig.name
  tags: eventhubsConfig.resourceTags
  location: eventhubsConfig.location
  sku: {
    name: eventhubsConfig.sku
    tier: eventhubsConfig.sku
    capacity: eventhubsConfig.capacity
  }

  properties: {
    minimumTlsVersion: minTlsVersion
  }

  resource observationEventHub 'eventhubs@2023-01-01-preview' = {
    name: observationEventHubName
    properties: {
      messageRetentionInDays: eventhubsConfig.messageRetentionDays
      partitionCount: eventhubsConfig.partitionCount
    }
  }

  resource interactionEventHub 'eventhubs@2023-01-01-preview' = {
    name: interactionEventHubName
    properties: {
      messageRetentionInDays: eventhubsConfig.messageRetentionDays
      partitionCount: eventhubsConfig.partitionCount
    }
  }
}

// Add role assignment to event hub receiver
resource eventHubRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (!empty(eventhubsConfig.roleAssignmentPrincipalId)) {
  name: guid(subscription().subscriptionId, 'AzureEventHubDataReceiver', eventhubsConfig.name)
  scope: eventHub
  properties: {
    principalId: eventhubsConfig.roleAssignmentPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a638d3c7-ab3a-418d-83e6-5f17a39d4fde')
  }
}
