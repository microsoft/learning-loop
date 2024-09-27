[CmdletBinding()]
param(
   [Parameter()]
   [switch] $noDeploy,

   [Parameter()]
   [switch] $skipSetupEnvironemt,

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

function KeyVaultExists {
   $keyVault = az keyvault show --name $imageRegistryKeyVaultName --resource-group $resourceGroupName --query "name" --output tsv 2>$null
   if ($LASTEXITCODE -ne 0) {
      return $false
   }
   return $null -ne $keyVault
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
   $account = GetAzAccount
   $normalizedLoopName = GetNormalizedLoopName $loopName
   if ($normalizedLoopName -ne $loopName) {
      $loopName = $normalizedLoopName
      Write-Host "Adjusted loop name to: '$loopName'" -ForegroundColor Yellow
   }
   
   # Set default value for acrName if dockerUserOrOrgName is not provided
   if ([string]::IsNullOrEmpty($dockerUserOrOrgName) -and [string]::IsNullOrEmpty($acrName)) {
      $script:acrName = "acrlearningloop"
      Write-Host "ACR name defaulted to '$acrName'" -ForegroundColor Yellow
   }
   
   if ($imageRegistryCredType -eq "KeyVault") {
      if ([string]::IsNullOrEmpty($imageRegistryKvSubscriptionId)) {
         $script:imageRegistryKvSubscriptionId = $account.id
         Write-Host "KeyVault subscription defaulted to '$imageRegistryKvSubscriptionId'" -ForegroundColor Yellow
      }
      if ([string]::IsNullOrEmpty($imageRegistryKvUserNameId)) {
         $script:imageRegistryKvUserNameId = "$loopName-username"
         Write-Host "KeyVault username id defaulted to '$imageRegistryKvUserNameId'" -ForegroundColor Yellow 
      }
      if ([string]::IsNullOrEmpty($imageRegistryKvPasswordId)) {
         $script:imageRegistryKvPasswordId = "$loopName-password"
         Write-Host "KeyVault password id defaulted to '$imageRegistryKvPasswordId'" -ForegroundColor Yellow
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
      throw "You must specify either -dockerUserOrOrgName or -acrName."
   }

   Write-Host "Docker repository is '$script:imageHost'" -ForegroundColor Yellow
   Write-Host ""
}

function DisplayParameters {
   $paramterList = @{};

   Write-Host "The deployment will execute with the following parameters:" -ForegroundColor Yellow
   if ($noDeploy -eq $true) {
      $paramterList["noDeploy"] = @{ Value = $noDeploy; Note = "Generates the bicep params file ${bicepParamsFile}, but will NOT deploy the resources" }
   }
   else {
      $paramterList["noDeploy"] = @{ Value = $noDeploy; Note = "Generate the bicep params file ${bicepParamsFile}, AND will deploy the resources" }
   }
   if ($loopNameAdjusted) {
      $paramterList["loopName"] = @{ Value = $loopName; Note = "Adjusted loopName for deployment" }
   }
   else {
      $paramterList["loopName"] = @{ Value = $loopName; Note = "" }
   }

   if ($skipSetupEnvironemt -eq $true) {
      $paramterList["skipSetupEnvironemt"] = @{ Value = $skipSetupEnvironemt; Note = "Will not deploy the resource group (it must exist)" }
   }
   else {
      $paramterList["skipSetupEnvironemt"] = @{ Value = $skipSetupEnvironemt; Note = "Create the resource group and dependencies" }
   }

   $paramterList["enableTrainer"] = @{ Value = $enableTrainer; Note = "" }
   $paramterList["enableJoiner"] = @{ Value = $enableJoiner; Note = "" }
   $paramterList["resourceGroupName"] = @{ Value = $resourceGroupName; Note = "" }
   $paramterList["location"] = @{ Value = $location; Note = "" }
   $paramterList["loadAndPushDockerImage"] = @{ Value = $loadAndPushDockerImage; Note = "" }
   $paramterList["dockerImageTar"] = @{ Value = $dockerImageTar; Note = "" }
   $paramterList["dockerImageName"] = @{ Value = $dockerImageName; Note = "" }
   $paramterList["dockerImageTag"] = @{ Value = $dockerImageTag; Note = "" }
   $paramterList["dockerUserOrOrgName"] = @{ Value = $dockerUserOrOrgName; Note = "" }
   $paramterList["acrName"] = @{ Value = $acrName; Note = "" }
   $paramterList["imageRegistryCredType"] = @{ Value = $imageRegistryCredType; Note = "" }

   if ($imageRegistryCredType -eq "KeyVault") {
      $paramterList["imageRegistryKeyVaultName"] = @{ Value = $imageRegistryKeyVaultName; Note = "" }
      $paramterList["imageRegistryKvSubscriptionId"] = @{ Value = $imageRegistryKvSubscriptionId; Note = "" }
      $paramterList["imageRegistryKvUserNameId"] = @{ Value = $imageRegistryKvUserNameId; Note = "" }
      $paramterList["imageRegistryKvPasswordId"] = @{ Value = $imageRegistryKvPasswordId; Note = "" }
   }
   elseif ($imageRegistryCredType -eq "ManagedIdentity") {
      $paramterList["imageRegistryManagedIdentityName"] = @{ Value = $imageRegistryManagedIdentityName; Note = "" }
   }
   elseif ($imageRegistryCredType -eq "Classic") {
      $paramterList["imageRegistryUserName"] = @{ Value = $imageRegistryPassword; Note = "You will be prompted for the username" }
      $paramterList["imageRegistryPassword"] = @{ Value = $imageRegistryPassword; Note = "You will be prompted for the password" }
   }
   $paramterList | Format-Table -Property @{Label="Parameter"; Expression={$_.Key}}, @{Label="Value"; Expression={$_.Value.Value}}, @{Label="Note"; Expression={$_.Value.Note}} -AutoSize
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
         host: '$script:imageHost'
         credentials: $imageCredentials
      }
      name: '$dockerImageName'
      tag: '$dockerImageTag'
      }
   }
}
"@
   
   $bicepParams | Out-File -FilePath $bicepParamsFile -Encoding utf8
   Write-Host "Bicep parameters file generated: $bicepParamsFile" -ForegroundColor Yellow
}

function TryVerifyDocker {
   docker info >$null 2>&1
   if ($LASTEXITCODE -ne 0) {
      throw "Failed to start Docker. Please start Docker and try again."
   }
   else {
      Write-Host "Docker is running" -ForegroundColor Green
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
      $fullDockerImage = "${script:imageHost}/${dockerImageName}:${dockerImageTag}"
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

function PromptSecure {
   param (
      [string]$prompt
   )

   $secureString = Read-Host -Prompt $prompt -AsSecureString
   $plainText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureString))
   return $plainText
}

function TrySetupEnvironment {
   Write-Host "Deploying environment..."
   $createKeyVault = ($imageRegistryCredType -eq "KeyVault") -and ((KeyVaultExists) -eq $false)
   if ($createKeyVault) {
      $registryUsername = PromptSecure -Prompt "Enter the username for the image registry"
      $registryPassword = PromptSecure -Prompt "Enter the password for the image registry"

      $userObjectId = (az ad signed-in-user show --query objectId --output tsv)
      $azResult = az deployment sub create `
      --location $location `
      --name "$loopName-environment" `
      --template-file .\environment.bicep `
      --parameters resourceGroupName=$resourceGroupName managedIdentityName=$imageRegistryManagedIdentityName keyVaultName=$imageRegistryKeyVaultName imageRegistryUsernameId=$imageRegistryKvUserNameId imageRegistryUsername=$registryUsername imageRegistryPasswordId=$imageRegistryKvPasswordId imageRegistryPassword=$registryPassword userObjectId=$userObjectId
      $registryPassword = $null
      $registryUsername = $null
      }
   else {
      $azResult = az deployment sub create `
      --location $location `
      --name "$loopName-environment" `
      --template-file .\environment.bicep `
      --parameters resourceGroupName=$resourceGroupName acrName=$acrName managedIdentityName=$imageRegistryManagedIdentityName
   }
   $azResultJson = $azResult | ConvertFrom-Json
   if ($azResultJson.properties.provisioningState -eq "Succeeded") {
      Write-Output "Environment deployment succeeded."
   }
   else {
      throw "Environment deployment failed: $azResultJson"
   }
}

function TryDeployLoop {
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

function TryVerifyAccountInfo {
   $account = GetAzAccount
   Write-Host "Logged in as: " -NoNewline
   Write-Host "$($account.user.name) ($($account.user.type))" -ForegroundColor Green
   Write-Host "Subscription: " -NoNewline
   Write-Host "$($account.name) - $($account.id)" -ForegroundColor Green
   Write-Host ""
}

#############################################################################
# Main script
try {
   TryVerifyDocker
   TryVerifyAccountInfo
   ValidateAndDefaultParameters
   DisplayParameters
   GeneratedParametersFile

   if ($noDeploy) {
      Write-Host "Done... skipping deployment (noDeploy: $noDeploy)" -ForegroundColor Yellow
      exit 0
   }

   $continue = Read-Host "Would you like to continue with the deployment? (yes/no)"
   if ($continue -ne "yes") {
      Write-Host "Deployment aborted by the user." -ForegroundColor Yellow
      exit 0
   }

   if ($skipSetupEnvironemt -eq $false) {
      TrySetupEnvironment
   }

   if ($loadAndPushDockerImage -eq $true) {
      TryPushDockerImage
   }

   TryDeployLoop
}
catch {
    Write-Host "An error occurred: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Full error details: $($_ | Out-String)"  -ForegroundColor Red
    exit 1
}
