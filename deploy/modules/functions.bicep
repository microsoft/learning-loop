@export()
func makeResourceName(prefix string, suffix string) string => '${prefix}${uniqueString(resourceGroup().id)}${suffix}'

// storage account name is restricted to 24 characters (8 from the prefix, 13 from unique string, 3 from the suffix)
@export()
func makeStorageAccountName(prefix string) string => makeResourceName(take(prefix, 8), 'stg')

// eventhub namespace name is restricted to 50 characters (35 from the prefix, 13 from unique string, 2 from the suffix)
@export()
func makeEventHubName(prefix string) string => makeResourceName(take(prefix, 35), 'eh')

@export()
func makeStorageAccountUrl(storageAccountName string) string => 'https://${storageAccountName}.blob.${environment().suffixes.storage}'

@export()
func makeEventhubNamespace(eventHubName string) string => '${eventHubName}.servicebus.windows.net'

@export()
func makeAppContainerGroupName(prefix string) string => makeResourceName(take(prefix, 48), 'cg')

@export()
func makeContainerImagePath(server string, imageName string, imageTag string) string => '${server}/${imageName}:${imageTag}'
