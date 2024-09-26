[CmdletBinding()]
param(
   [Parameter()]
   [switch] $noDeploy,

   [Parameter()]
   [string] $bicepParamsFile = "parameters.bicepparam",

   [Parameter()]
   [string] $loopName = "sample-loop",

   [Parameter()]
   [bool] $enableTrainer = $true,

   [Parameter()]
   [bool] $enableJoiner = $true,

   [Parameter()]
   [string] $resourceGroupName = "rg-sample-loop",

   [Parameter()]
   [string] $location = "westus2",

   [Parameter()]
   [bool] $loadAndPushDockerImage = $true,

   [Parameter()]
   [ValidateScript({
      if ($loadAndPushDockerImage -and $null -eq $args[0]) {
          throw "You must specify -dockerImageTar if -loadAndPushDockerImage is true."
      }
      $true
   })]
   [string] $dockerImageTar,

   [Parameter()]
   [string] $dockerImageName = "learning-loop",

   [Parameter()]
   [string] $dockerImageTag = "latest",

   [Parameter()]
   [ValidateScript({
      if ($null -ne $args[0] -and $null -ne $acrName) {
            throw "You cannot specify both -dockerUserOrOrgName and -acrName."
      }
      $true
   })]
   [string] $dockerUserOrOrgName,

   [Parameter()]
   [ValidateScript({
      if ($null -ne $args[0] -and $null -ne $dockerUserOrOrgName) {
            throw "You cannot specify both -dockerUserOrOrgName and -acrName."
      }
      $true
   })]
   [string] $acrName,

   [Parameter()]
   [ValidateSet("ManagedIdentity", "KeyVault", "Classic")]
   [string] $imageRegistryCredType = "ManagedIdentity",

   [ValidateScript({
      if ($imageRegistryCredType -eq "KeyVault" -or $null -eq $args[0]) {
          $true
      } else {
          throw "You can only specify -imageRegistryKeyVaultName if -imageRegistryCredType is 'KeyVault'."
      }
   })]
   [string] $imageRegistryKeyVaultName = "kv-learning-loop",

   [Parameter()]
   [ValidateScript({
      if ($imageRegistryCredType -eq "KeyVault" -or $null -eq $args[0]) {
          $true
      } else {
          throw "You can only specify -imageRegistryKvSubscriptionId if -imageRegistryCredType is 'KeyVault'."
      }
   })]
   [string] $imageRegistryKvSubscriptionId,

   [Parameter()]
   [ValidateScript({
      if ($imageRegistryCredType -eq "KeyVault" -or $null -eq $args[0]) {
          $true
      } else {
          throw "You can only specify -imageRegistryKvUserNameId if -imageRegistryCredType is 'KeyVault'."
      }
   })]
   [string] $imageRegistryKvUserNameId,

   [Parameter()]
   [ValidateScript({
      if ($imageRegistryCredType -eq "KeyVault" -or $null -eq $args[0]) {
          $true
      } else {
          throw "You can only specify -imageRegistryKvPasswordId if -imageRegistryCredType is 'KeyVault'."
      }
   })]
   [string] $imageRegistryKvPasswordId,

   [Parameter()]
   [ValidateScript({
      if ($imageRegistryCredType -eq "ManagedIdentity" -or $null -eq $args[0]) {
          $true
      } else {
          throw "You can only specify imageRegistryManagedIdentityName if -imageRegistryCredType is 'ManagedIdentity'."
      }
   })]
   [string] $imageRegistryManagedIdentityName = "mi-learning-loop",

   [Parameter()]
   [ValidateScript({
      if ($imageRegistryCredType -eq "Classic" -or $null -eq $args[0]) {
          $true
      } else {
          throw "You can only specify imageRegistryUserName if -imageRegistryCredType is 'Classic'."
      }
   })]
   [SecureString] $imageRegistryUserName,

   [Parameter()]
   [ValidateScript({
      if ($imageRegistryCredType -eq "Classic" -or $null -eq $args[0]) {
          $true
      } else {
          throw "You can only specify imageRegistryPassword if -imageRegistryCredType is 'Classic'."
      }
   })]
   [SecureString] $imageRegistryPassword
)

# Globals -- these are used in the functions below (TODO: refactor to pass as parameters)
$imageHost = ""

function GetAzAccount {
   $account = az account show --output json | ConvertFrom-Json
   if ($account) {
      Write-Output "Logged in as $($account.user.name) ($($account.user.type))"
      Write-Output "Subscription: $($account.id) - $($account.name)"
      return $account
   }
   else {
      throw "Not logged in. Please login using 'az login'."
   }
}

function GetNormalizedLoopName {
   param (
      [string]$loopName
   )

   $adjustedLoopName = $loopName.ToLower()
   $adjustedLoopName = $adjustedLoopName -replace '_', '-' # change _ to -
   $adjustedLoopName = $adjustedLoopName -replace '[^a-zA-Z0-9-]', '' # Remove invalid characters
   $adjustedLoopName = $adjustedLoopName -replace '--+', '-' # Replace consecutive dashes with a single dash
   $adjustedLoopName = $adjustedLoopName.Trim('-') # Remove leading and trailing dashes
   if ($adjustedLoopName -match '^[^a-zA-Z]') {
      $adjustedLoopName = "a$adjustedLoopName" # Ensure it starts with a letter
   }
   return $adjustedLoopName
}

function ValidateAndDefaultParameters {
   $normalizedLoopName = GetNormalizedLoopName $loopName
   if ($normalizedLoopName -ne $loopName) {
      $loopName = $normalizedLoopName
      Write-Output "Adjusted loop name to: '$loopName'."
   }
   
   # Set default value for acrName if dockerUserOrOrgName is not provided
   if ([string]::IsNullOrEmpty($dockerUserOrOrgName) -and [string]::IsNullOrEmpty($acrName)) {
      $script:acrName = "acrlearningloop"
      Write-Output "ACR name defaulted to '$acrName'."
   }
   
   if ($imageRegistryCredType -eq "KeyVault") {
      if ([string]::IsNullOrEmpty($imageRegistryKvSubscriptionId)) {
         $script:imageRegistryKvSubscriptionId = $account.id
         Write-Output "KeyVault subscription defaulted to '$imageRegistryKvSubscriptionId'"
      }
      if ([string]::IsNullOrEmpty($imageRegistryKvUserNameId)) {
         $script:imageRegistryKvUserNameId = "$loopName-username"
         Write-Output "KeyVault username id defaulted to '$imageRegistryKvUserNameId'"
      }
      if ([string]::IsNullOrEmpty($imageRegistryKvPasswordId)) {
         $script:imageRegistryKvPasswordId = "$loopName-password"
         Write-Output "KeyVault password id defaulted to '$imageRegistryKvPasswordId'"
      }
   }

   # TODO: fix $script:imageHost
   if ([string]::IsNullOrEmpty($dockerUserOrOrgName) -eq $false) {
      $script:imageHost = "docker.io"
   }
   elseif ($null -ne $acrName) {
      $script:imageHost = "$acrName.azurecr.io"
   }
   else {
      Write-Output "You must specify either -dockerUserOrOrgName or -acrName."
      exit 1
   }
}

function DisplayParameters {
   Write-Output "The deployment will execute with the following parameters:"
   if ($noDeploy -eq $true) {
      Write-Output "noDeploy: $noDeploy - this will generate the bicep params file $bicepParamsFile, but will NOT deploy the resources."
   }
   else {
      Write-Output "noDeploy: $noDeploy - this will generate the bicep params file $bicepParamsFile, AND will deploy the resources."
   }
   if ($loopNameAdjusted) {
      Write-Output "loopName: $loopName (adjusted to $normalizedLoopName)"
   }
   else {
      Write-Output "loopName: $loopName"
   }
   Write-Output "enableTrainer: $enableTrainer"
   Write-Output "enableJoiner: $enableJoiner"
   Write-Output "resourceGroupName: $resourceGroupName"
   Write-Output "location: $location"
   Write-Output "loadAndPushDockerImage: $loadAndPushDockerImage"
   Write-Output "dockerImageTar: $dockerImageTar"
   Write-Output "dockerImageName: $dockerImageName"
   Write-Output "dockerImageTag: $dockerImageTag"
   Write-Output "dockerUserOrOrgName: $dockerUserOrOrgName"
   Write-Output "acrName: $acrName"
   Write-Output "imageRegistryCredType: $imageRegistryCredType"
   if  ($imageRegistryCredType -eq "KeyVault") {
      Write-Output "imageRegistryKeyVaultName: $imageRegistryKeyVaultName"
      Write-Output "imageRegistryKvSubscriptionId: $imageRegistryKvSubscriptionId"
      Write-Output "imageRegistryKvUserNameId: $imageRegistryKvUserNameId"
      Write-Output "imageRegistryKvPasswordId: $imageRegistryKvPasswordId"
   }
   elseif ($imageRegistryCredType -eq "ManagedIdentity") {
      Write-Output "imageRegistryManagedIdentityName: $imageRegistryManagedIdentityName"
   }
   elseif ($imageRegistryCredType -eq "Classic") {
      Write-Output "You will prompted for the imageRegistryPassword and imageRegistryUserName"
   }
}

function GeneratedParametersFile {
   $keyVaultParams = "";
   if ($imageRegistryCredType -eq "KeyVault") {
      $keyVaultParams = @"
param kvImageRegistryUsername = getSecret('$imageRegistryKvSubscriptionId', '$resourceGroupName', '$imageRegistryKeyVaultName', '$imageRegistryKvUserNameId')
param kvImageRegistryPassword = getSecret('$imageRegistryKvSubscriptionId', '$resourceGroupName', '$imageRegistryKeyVaultName', '$imageRegistryKvPasswordId')
"@
   }
   
   $imageCredentials = "";
   if ($imageRegistryCredType -eq "ManagedIdentity") {
      $imageCredentials = @"
{
   type: 'managedIdentity'
   username: '$imageRegistryManagedIdentityName'
}
"@
   }
   elseif ($imageRegistryCredType -eq "KeyVault") { 
      $imageCredentials = @"
{
   type: 'keyVault'
}
"@
   }
   elseif ($imageRegistryCredType -eq "Classic") { 
      $imageCredentials = @"
{
   type: 'usernamePassword'
   username: '$imageRegistryUserName'
   password: '$imageRegistryPassword'
}
"@
   }
   
   $bicepParams = @"
//
// bicep pameters file generated by 'deploy-sample.ps1'
//
using 'main.bicep'
$keyVaultParams
param mainConfig = {
   appName: '$loopName'
   environmentVars: [
      {
      name: 'ExperimentalUnitDuration'
      value: '0:0:10'
      }
      {
      name: 'TrainerEnabled'
      value: $($enableTrainer.ToString().ToLower())
      }
      {
      name: 'JoinerEnabled'
      value: $($enableJoiner.ToString().ToLower())
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
   resourceTags: {
      deploymentGroupName: '$loopName'
   }
   storage: {
      sku: 'Standard_LRS'
      kind: 'StorageV2'
   }
   eventhub: {
      capacity: 1
      partitionCount: 4
      sku: 'Standard'
      messageRetentionDays: 1
   }
   container: {
      cpuCores: 4
      memoryGig: 16
      image: {
      registry: {
         host: '$Global:imageHost'
         credentials: $imageCredentials
      }
      name: '$dockerImageName'
      tag: '$dockerImageTag'
      }
   }
}
"@
   
   $bicepParams | Out-File -FilePath $bicepParamsFile -Encoding utf8
   Write-Output "Bicep parameters file generated: $bicepParamsFile"
}

function TryStartDocker {
   docker info >$null 2>&1
   if ($LASTEXITCODE -ne 0) {
      Write-Output "Docker is not running. Attempting to start Docker..."
      if ($IsWindows) {
         Start-Service -Name "docker"
      }
      else {
         sudo systemctl start docker
      }
      Start-Sleep -Seconds 10 # Wait for Docker to start

      # Re-check if Docker is running
      docker info >$null 2>&1
      if ($LASTEXITCODE -ne 0) {
         Write-Output "Docker is still not running. Waiting 10 more seconds..."
         Start-Sleep -Seconds 10 # Wait additional 10 seconds

         docker info >$null 2>&1
         if ($LASTEXITCODE -ne 0) {
            throw "Failed to start Docker. Please start the Docker application manually."
         }
         else {
            Write-Output "Docker started successfully."
         }
      }
      else {
         Write-Output "Docker started successfully."
      }
   }
   else {
      Write-Output "Docker is running."
   }
}

function TryCreateResourceGroup {
   $resourceGroup = az group show --name $resourceGroupName --query "name" --output tsv 2>$null
   if ($null -eq $resourceGroup) {
      az group create --name $resourceGroupName --location $location --output none
      Write-Output "Resource group '$resourceGroupName' created in location '$location'"
   }
   else {
      Write-Output "Resource group '$resourceGroupName' exists; skipping creation"
   }
}

function TryCreateCredentials {
   # setup image registry credentials
   if ($imageRegistryCredType -eq "ManagedIdentity") {
      $identity = az identity show --name $imageRegistryManagedIdentityName --resource-group $resourceGroupName --query "name" --output tsv 2>$null
      if ($null -eq $identity) {
         Write-Output "Creating Managed Identity '$imageRegistryManagedIdentityName' in resource group '$resourceGroupName'."
         $result = az identity create --name $imageRegistryManagedIdentityName --resource-group $resourceGroupName --location $location
         if ($result) {
            Write-Output "Managed Identity '$imageRegistryManagedIdentityName' created in resource group '$resourceGroupName'."
         }
         else {
            throw "Failed to create Managed Identity '$imageRegistryManagedIdentityName' in resource group '$resourceGroupName'."
         }
      }
      else {
         Write-Output "Managed Identity '$imageRegistryManagedIdentityName' already exists in resource group '$resourceGroupName'."
      }
   }
   elseif ($imageRegistryCredType -eq "KeyVault") {
      $keyVault = az keyvault show --name $imageRegistryKeyVaultName --resource-group $resourceGroupName --query "name" --output tsv 2>$null
      if ($null -eq $keyVault) {
         Write-Output "Creating KeyVault '$imageRegistryKeyVaultName' in resource group '$resourceGroupName'."
         $result = az keyvault create --name $imageRegistryKeyVaultName --resource-group $resourceGroupName --location $location
         if ($result) {
            Write-Output "KeyVault '$imageRegistryKeyVaultName' created in resource group '$resourceGroupName'."
         }
         else {
            throw "Failed to create KeyVault '$imageRegistryKeyVaultName' in resource group '$resourceGroupName'."
         }
      }
      else {
         Write-Output "KeyVault '$imageRegistryKeyVaultName' already exists in resource group '$resourceGroupName'."
      }
   
      $secretUsername = Read-Host -Prompt "Enter your username" -AsSecureString
      $secretPassword = Read-Host -Prompt "Enter your password" -AsSecureString
      $usernamePlainText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secretUsername))
      $passwordPlainText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secretPassword))
      $principalId = az ad signed-in-user show --query objectId --output tsv
      if ($principalId) {
         # todo: check success of the following
         az keyvault set-policy --name $imageRegistryKeyVaultName --object-id $principalId --secret-permissions set get
         az keyvault secret set --vault-name $imageRegistryKeyVaultName --name $imageRegistryKvUserNameId --value $usernamePlainText
         az keyvault secret set --vault-name $imageRegistryKeyVaultName --name $imageRegistryKvPasswordId --value $passwordPlainText
      }
      $usernamePlainText = $null
      $passwordPlainText = $null
   }
   elseif ($imageRegistryCredType -eq "Classic") {
      Write-Output "Using classic username/password for image registry authentication."
   }
}

function TryCreateACR {
   $acr = az acr show --name $acrName --resource-group $resourceGroupName --query "name" --output tsv 2>$null
   if ($null -eq $acr) {
      Write-Output "Creating ACR '$acrName' in resource group '$resourceGroupName'."
      $result = az acr create --name $acrName --resource-group $resourceGroupName --sku Basic
      if ($result) {
         Write-Output "ACR '$acrName' created in resource group '$resourceGroupName'."
   
         # Grant the managed identity ACR pull access
         $principalId = az identity show --name $imageRegistryManagedIdentityName --resource-group $resourceGroupName --query "principalId" --output tsv
         $acrScope = az acr show --name $acrName --resource-group $resourceGroupName --query id --output tsv
         Write-Output "Granting managed identity '$imageRegistryManagedIdentityName' ACR pull access (princialId: $principalId, scope: $acrScope)."
         $roleAssignmentResult = az role assignment create --assignee $principalId --role "AcrPull" --scope $acrScope
         if ($roleAssignmentResult) {
            Write-Output "Managed identity granted ACR pull access successfully."
         }
         else {
            throw "Failed to grant ACR pull access to the managed identity."
         }
      }
      else {
         throw "Failed to create ACR '$acrName' in resource group '$resourceGroupName'."
      }
   }
   else {
      Write-Output "ACR '$acrName' already exists in resource group '$resourceGroupName'."
   }
}

function TryPushDockerImage
{
   docker load -i $dockerImageTar
   if ($LASTEXITCODE -eq 0) {
      Write-Output "Docker image loaded successfully."
   } else {
      throw "Failed to load Docker image."
   }

   $fullDockerImage = "";
   if ([string]::IsNullOrEmpty($dockerUserOrOrgName) -eq $false) {
      docker login
      if ($LASTEXITCODE -eq 0) {
         Write-Output "Docker login succeeded."
      } else {
         throw "Docker login failed."
      }
      $fullDockerImage = "$dockerUserOrOrgName/$dockerImageName"
   }
   elseif ([string]::IsNullOrEmpty($acrName) -eq $false) {
      az acr login --name $acrName
      if ($LASTEXITCODE -eq 0) {
         Write-Output "Azure Container Registry login succeeded."
      } else {
         throw "Azure Container Registry login failed."
      }
      $fullDockerImage = "${Global:imageHost}/${dockerImageName}:${dockerImageTag}"
   }
   else {
      throw "You must specify either -dockerUserOrOrgName or -acrName."
   }

   docker tag ${dockerImageName}:${dockerImageTag} $fullDockerImage
   if ($LASTEXITCODE -eq 0) {
      Write-Output "Docker tag ${dockerImageName}:${dockerImageTag} -> $fullDockerImage succeeded."
   } else {
      throw "Docker tag ${dockerImageName}:${dockerImageTag} -> $fullDockerImage failed."
   }

   Write-Output "Pushing docker image to acr... $fullDockerImage"
   docker push $fullDockerImage
   if ($LASTEXITCODE -eq 0) {
      Write-Output "Docker push $fullDockerImage succeeded."
   } else {
      throw "Docker push $fullDockerImage failed."
   }
}

function TryDeploy {
   Write-Output "Deployment started... using bicep parameters file: $bicepParamsFile"
   $azResult = az deployment group create --resource-group $resourceGroupName --name $loopName --parameters $bicepParamsFile
   $azResultJson = $azResult | ConvertFrom-Json
   if ($azResultJson.properties.provisioningState -eq "Succeeded") {
      Write-Output "Deployment succeeded."
   }
   else {
      throw "Deployment failed: $azResultJson" 
   }
}

#############################################################################
# Main script
try {
   $account = GetAzAccount
   Write-Output "Logged in as $($account.user.name) ($($account.user.type))"
   Write-Output "Subscription: $($account.id) - $($account.name)"
   ValidateAndDefaultParameters
   DisplayParameters
   GeneratedParametersFile

   if ($noDeploy) {
      Write-Output "Skipping deployment."
      exit 0
   }

   $continue = Read-Host "Would you like to continue with the deployment? (yes/no)"
   if ($continue -ne "yes") {
      Write-Output "Deployment aborted by the user."
      exit 0
   }

   TryStartDocker
   TryCreateResourceGroup
   TryCreateCredentials
   TryCreateACR 

   if ($loadAndPushDockerImage -eq $true) {
      TryPushDockerImage
   }

   TryDeploy
}
catch {
    Write-Output "An error occurred: $($_.Exception.Message)"
    Write-Output "Full error details: $($_ | Out-String)"
    exit 1
}
