// storage account setup
@export()
type storageSkuType = 'Premium_LRS' | 'Premium_ZRS' | 'Standard_GRS' | 'Standard_GZRS' | 'Standard_LRS' | 'Standard_RAGRS' | 'Standard_RAGZRS' | 'Standard_ZRS'
@export()
type storageKindType = 'BlobStorage' | 'BlockBlobStorage' | 'FileStorage' | 'Storage' | 'StorageV2'

type storageConfigT = {
  @description('Name of the storage account')
  name: string
  @description('Tags applied to each deployed resource')
  resourceTags: object?
  @description('Location for the storage account')
  location: string
  @description('SKU for the storage account (e.g. Standard_LRS)')
  sku: storageSkuType
  @description('Kind of storage account (e.g. StorageV2)')
  kind: storageKindType
  @description('Name of the blob container to create')
  blobContainerName: string
  @description('Principal ID for the role assignments')
  roleAssignmentPrincipalId: string
}

param storageConfig storageConfigT

// create the storage account
resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' = {
  name: storageConfig.name
  location: storageConfig.location
  tags: storageConfig.resourceTags
  sku: {
    name: storageConfig.sku
  }
  kind: storageConfig.kind
  properties: {
    allowBlobPublicAccess: false
    accessTier: 'Hot'
  }

  // create blob services
  resource blob_stg 'blobServices@2019-06-01' = {
    name: 'default'
    resource appContainer 'containers@2019-06-01' = {
      name: storageConfig.blobContainerName
      properties: {
        publicAccess: 'None'
      }
    }
  }
}

// Add role assignment to storage account for Storage Blob Data Contributor
resource storageRoleAssigment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (!empty(storageConfig.roleAssignmentPrincipalId)) {
  name: guid(subscription().subscriptionId, 'StorageBlobDataContributor', storageConfig.name)
  scope: storageAccount
  properties: {
    principalId: storageConfig.roleAssignmentPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  }
}
