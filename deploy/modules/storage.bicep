// Learning Loop storage account setup
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

output storageAccountName string = storageAccount.name
output storageBlobEndpoint string = '${storageAccount.properties.primaryEndpoints.blob}${storageConfig.blobContainerName}'
