@export()
func makeResourceName(prefix string, suffix string) string => '${prefix}${suffix}'

@export()
func makeStorageAccountName(prefix string) string => makeResourceName(prefix, 'stg')

@export()
func makeEventHubName(prefix string) string => makeResourceName(prefix, 'eh')

@export()
func makeStorageAccountUrl(storageAccountName string) string => 'https://${storageAccountName}.blob.${environment().suffixes.storage}'

@export()
func makeEventhubNamespace(eventHubName string) string => '${eventHubName}.servicebus.windows.net'

@export()
func makeAppContainerGroupName(prefix string) string => makeResourceName(prefix, 'cg')

@export()
func makeContainerImagePath(server string, imageName string, imageTag string) string => '${server}/${imageName}:${imageTag}'
