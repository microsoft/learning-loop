<#
.SYNOPSIS
   Deploys a sample Learning Loop environment.

.DESCRIPTION
   This script deploys a sample Learning Loop environment using specified parameters for location,
   environment parameters file, main deployment parameters file, Docker image file, RL simulation
   configuration type, RL simulation configuration file, and the expected archived image name.

   A successful deployment will create/overwrite a file named "main-deploy.bicepparam" (default) in the current directory,
   and a RL simulation configuration file "rl-sim-config.json" (default) in the current directory.

   Prior to running this script, you must have the following:

      1) The Azure CLI and the Azure Bicep tools installed and in the path.  An current Azure CLI
         will include Azure Bicep.
      2) You must be logged in to the Azure CLI with a user account that has the required permissions to
         create a resource group, storage account, event hub, application insights, and a managed identity.
      3) Docker Engine installed and running.
      4) The Leaning Loop docker image file avialable on the filesystem.

.PARAMETER location
   The Azure location where the resources will be deployed.
   Default: "westus2"

.PARAMETER environmentParamsFile
   Path to the environment parameters file.
   Default: './sample-loop-environment.acr.bicepparam'
   Validation: Must be a valid path.

.PARAMETER mainDeployParamsFile
   Path to the main deployment parameters file.
   Default: "./main-deploy.bicepparam"
   This file will be created/overwritten with the output of the environment deployment.

.PARAMETER dockerImageFile
   Path to the Docker image file.
   Default: "./docker-image-ubuntu-latest.zip"
   Validation: Must be a valid path.

.PARAMETER rlSimConfigType
   Type of RL simulation configuration.
   Default: "az"
   Allowed Values: "az", "connstr"

.PARAMETER rlSimConfigFile
   Path to the RL simulation configuration file.
   Default: "./rl-sim-config.json"
   This file will be created/overwritten with the output of the environment deployment.

.PARAMETER expectedArchivedImageName
   The expected name of the archived image. This name is used to identify the extracted image 
   from a zipped or gzipped Docker archive.
   Default: "./learning-loop-ubuntu-latest"

.PARAMETER initStorage
   Initialize the storage account to allow rl_sim to start. Use this switch if you are running
   rl_sim manually and a model does not already exist in the storage account.
   Default: false

.EXAMPLE
   .\deploy-sample.ps1
   Deploys the sample Learning Loop environment to the "westus2" location using the sample parameter file
   using the defaults.

.EXAMPLE
   .\deploy-sample.ps1 -location "eastus" -environmentParamsFile "./custom-environment.bicepparam"
   Deploys the sample Learning Loop environment to the "eastus" location using the specified environment parameters file.
#>
###############################################################################
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
###############################################################################
param(
   [string] $location = "westus2",
   [ValidateScript({
      if (-not (Test-Path $_)) {
          throw "The specified file '$($_)' does not exist. Please provide a valid file path."
      }
      $true
   })]
   [System.IO.FileInfo] $environmentParamsFile = './sample-loop-environment.acr.bicepparam',
   [string] $mainDeployParamsFile = "./main-deploy.bicepparam",
   [ValidateScript({
      if (-not (Test-Path $_)) {
          throw "The specified file '$($_)' does not exist. Please provide a valid file path."
      }
      $true
   })]
   [System.IO.FileInfo] $dockerImageFile = "./docker-image-ubuntu-latest.zip",
   [ValidateSet("az", "connstr")]
   [string]$rlSimConfigType = "az",
   [System.IO.FileInfo] $rlSimConfigFile = "./rl-sim-config.json",
   [string] $expectedArchivedImageName = "./learning-loop-ubuntu-latest",
   [switch] $initStorage = $false
)

<#
.SYNOPSIS
   Prompts the user for a secure string input and returns it as plain text.

.DESCRIPTION
   The Get-SecureString function prompts the user to enter a secure string input. 
   It then converts the secure string to plain text and returns it.

.PARAMETER prompt
   The message displayed to the user when prompting for input.

.RETURNS
   [string] The plain text representation of the secure string input.

.EXAMPLE
   $password = Get-SecureString -prompt "Enter your password"
   Write-Host "Your password is: $password"
#>
function Get-SecureString {
   param (
      [string]$prompt
   )

   $secureString = Read-Host -Prompt $prompt -AsSecureString
   $plainText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureString))
   return $plainText
}

<#
.SYNOPSIS
   Decompresses a Gzip file to a specified destination.

.DESCRIPTION
   The Expand-Gzip function takes a Gzip file and decompresses its contents to a specified destination file.
   It uses the System.IO.Compression.GzipStream class to handle the decompression process.

.PARAMETER gzipFile
   The path to the Gzip file that needs to be decompressed.

.EXAMPLE
   Expand-Gzip -gzipFile "C:\path\to\file.gz" -destination "C:\path\to\output\file.txt"
   This example decompresses the file.gz file and saves the output to file.txt.

.NOTES
   Ensure that the destination path has the necessary write permissions.
#>
function Expand-Gzip {
   param (
      [string] $gzipFile
   )
   $destination = [System.IO.Path]::ChangeExtension($gzipFile, $null)
   Write-Host "Decompressing $gzipFile to $destination" -ForegroundColor Yellow
   $gzipStream = $null
   try {
      $gzipStream = [System.IO.Compression.GzipStream]::new([System.IO.File]::OpenRead($gzipFile), [System.IO.Compression.CompressionMode]::Decompress)
      $outputFile = [System.IO.File]::Create($destination)
      $gzipStream.CopyTo($outputFile)
      $outputFile.Close()
   }
   catch {
      Write-Host "Failed to decompress $gzipFile to $destination" -ForegroundColor Red
   }
   if ($gzipStream) {
      $gzipStream.Dispose()
   }
}

<#
.SYNOPSIS
   Retrieves the URL of the latest Docker image artifact from a GitHub Actions workflow.

.DESCRIPTION
   The Get-LatestDockerImageUrl function fetches the URL of the latest successful Docker image artifact 
   from a specified GitHub Actions workflow. It uses the GitHub API to get the latest workflow run and 
   then filters the artifacts to find the one matching the specified name.

.PARAMETER None
   This function does not take any parameters.

.RETURNS
   [string] The URL of the latest Docker image artifact, or $null if no artifact is found.

.EXAMPLE
   $latestDockerImageUrl = Get-LatestDockerImageUrl
   Write-Host "The latest Docker image URL is: $latestDockerImageUrl"

.NOTES
   Ensure that the GitHub API rate limits are not exceeded when calling this function frequently.

   *********************************************************************************************
   A future implementation will not include this method as the docker image will come from
   a public Microsoft Container Registry and/or DockerHub.
   *********************************************************************************************
#>
function Get-LatestDockerImageUrl {
   try {
      $owner = "microsoft"
      $repo = "learning-loop"
      $workflowName = "build_all.yml"
      $artifactNameFilter = "docker-image-ubuntu-latest"
      $apiUrl = "https://api.github.com/repos/$owner/$repo/actions/workflows/$workflowName/runs?status=success&per_page=1"
      $headers = @{
         'User-Agent' = 'Powershell'
      }
      $workflowRuns = Invoke-RestMethod -Uri $apiUrl -Headers $headers
      $latestRunId = $workflowRuns.workflow_runs[0].id
   
      $artifactUrl = "https://api.github.com/repos/$owner/$repo/actions/runs/$latestRunId/artifacts"
      $artifacts = Invoke-RestMethod -Uri $artifactUrl -Headers $headers
      $artifact = $artifacts.artifacts | Where-Object { $_.name -eq $artifactNameFilter } | Select-Object -First 1
      if ($artifact) {
         $artifactId = $artifact.id
         return "https://github.com/$owner/$repo/actions/runs/$latestRunId/artifacts/$artifactId"
      } else {
         return $null
      }
   }
   catch {
      return $null
   }
}

<#
.SYNOPSIS
   Retrieves the Docker image tar file from a specified file path.

.DESCRIPTION
   The Get-DockerImageTar function checks if the specified Docker image file exists. 
   If the file does not exist, it retrieves the URL of the latest Docker image artifact 
   from GitHub Actions and throws an error. If the file exists, it determines the file type 
   and processes it accordingly. Supported file types are .tar, .zip, and .gz.

.PARAMETER dockerImageFile
   The path to the Docker image file. This parameter is mandatory.

.RETURNS
   [string] The path to the Docker image tar file.

.EXAMPLE
   $dockerImageTar = Get-DockerImageTar -dockerImageFile "C:\path\to\image.tar"
   Write-Host "The Docker image tar file is: $dockerImageTar"

.NOTES
   Ensure that the specified file path is correct and accessible.

   *********************************************************************************************
   A future implementation will not include this method as the docker image will come from
   a public Microsoft Container Registry and/or DockerHub.
   *********************************************************************************************
#>
function Get-DockerImageTar {
   param (
      [Parameter(Mandatory=$true)]
      [string]$dockerImageFile
   )
   if (-Not (Test-Path $dockerImageFile)) {
      $imagePackageUrl = Get-LatestDockerImageUrl
      throw "Cannot find docker image file $dockerImageFile.  Download the docker image file from the GitHub Actions artifacts page ($imagePackageUrl)."
   }
   $expectedGzFile = [System.IO.Path]::ChangeExtension($expectedArchivedImageName, ".tar.gz")
   $expectedTarFile = [System.IO.Path]::ChangeExtension($expectedArchivedImageName, ".tar")

   $imageFileType = [System.IO.Path]::GetExtension($dockerImageFile).ToLower()
   if ($imageFileType -eq ".tar") {
      return $dockerImageFile
   }
   elseif ($imageFileType -eq ".zip") {
      Expand-Archive -Path $dockerImageFile -DestinationPath . -Force
      if (Test-Path $expectedGzFile) {
         $expectedGzFile = (Get-Item $expectedGzFile).FullName
         Expand-Gzip $expectedGzFile
      }
      if (Test-Path $expectedTarFile) {
         $expectedTarFile = (Get-Item $expectedTarFile).FullName
         return $expectedTarFile
      }
      else {
         throw "Cannot find the docker image file in $dockerImageFile"
      }
   }
   elseif ($imageFileType -eq ".gz") {
      $expectedGzFile = (Get-Item $dockerImageFile).FullName
      $dockerImageTar = [System.IO.Path]::ChangeExtension($expectedGzFile, $null)
      Expand-Gzip $expectedGzFile
      if (-Not (Test-Path $dockerImageTar)) {
         throw "Cannot find the tar'd docker image file $dockerImageTar unpacked from $expectedGzFile"
      }
      return $dockerImageTar
   }
   else {
      throw "Unsupported image file type: $imageFileType"
   }
}

<#
.SYNOPSIS
   Retrieves the kind of image repository from a Bicep parameter file.

.DESCRIPTION
   The Get-ImageReposiotryKind function reads the content of a specified Bicep parameter file,
   removes comments and newlines, and then uses a regex pattern to extract the 'kind' property 
   of the image repository.

.PARAMETER bicepParamFile
   The path to the Bicep parameter file. This parameter is mandatory.

.RETURNS
   [string] The kind of the image repository.

.EXAMPLE
   $repositoryKind = Get-ImageReposiotryKind -bicepParamFile "C:\path\to\params.bicep"
   Write-Host "The image repository kind is: $repositoryKind"

.NOTES
   Ensure that the Bicep parameter file is correctly formatted and accessible.
#>
function Get-ImageReposiotryKind {
   param (
      [Parameter(Mandatory=$true)]
      [string] $bicepParamFile
   )
   $bicepParamContent = Get-Content -Path $bicepParamFile -Raw
   $bicepParamContent = $bicepParamContent -replace '//.*', ''
   $bicepParamContent = $bicepParamContent -replace '\r|\n', ''
   $pattern = "image\s*:\s*{[^{}]*properties\s*:\s*{[^{}]*?\bkind\s*:\s*'([^']*)'"
   if ($bicepParamContent -match $pattern) {
      return $Matches[1]
   }
   throw "Cannot find the image repository kind in the bicep parameter file $bicepParamFile"
}

<#
.SYNOPSIS
   Executes a command with logging and error handling.

.DESCRIPTION
   The Invoke-CmdWithLogging function runs a specified command scriptblock, logs the execution status, 
   and handles errors using a provided error handler scriptblock. It writes a message before executing 
   the command and indicates success or failure based on the command's exit code.

.PARAMETER message
   The message to display before executing the command.

.PARAMETER command
   The scriptblock containing the command to execute.

.PARAMETER errorHandler
   The scriptblock containing the error handling logic to execute if the command fails.

.RETURNS
   The return value of the executed command, or $null if the command fails.

.EXAMPLE
   Invoke-CmdWithLogging -message "Running script" -command { ./script.ps1 } -errorHandler { Write-Host "Error occurred" }
   This example runs the script.ps1 file, logs the execution status, and handles errors by writing an error message.
#>
function Invoke-CmdWithLogging {
   param (
      [Parameter(Mandatory=$true)]
      [string] $message,
      [Parameter(Mandatory=$true)]
      [scriptblock] $command,
      [scriptblock] $errorHandler
   )

   Write-Host "$message... " -NoNewline -ForegroundColor Yellow
   $retValue = & $command
   if ($LASTEXITCODE -ne 0) {
      if ($errorHandler) {
         & $errorHandler
      }
      return $null
   }
   Write-Host "Success" -ForegroundColor Green
   return $retValue
}

<#
.SYNOPSIS
   Initializes an Azure storage account by uploading a temporary file.

.DESCRIPTION
   The TryInitializeStorage function creates a temporary file and uploads it to a specified 
   Azure storage account and container. This is used to ensure that the storage account is 
   properly initialized. If the upload fails, an error message is displayed.

.PARAMETER storageAccountName
   The name of the Azure storage account to initialize.

.PARAMETER loopName
   The name of the container within the storage account where the file will be uploaded.

.RETURNS
   None

.EXAMPLE
   TryInitializeStorage -storageAccountName "mystorageaccount" -loopName "mycontainer"
   This example initializes the storage account "mystorageaccount" by uploading a temporary file 
   to the "mycontainer/exported-models" container.
#>
function TryInitializeStorage {
   param(
      [Parameter(Mandatory=$true)]
      [string] $storageAccountName,
      [Parameter(Mandatory=$true)]
      [string] $loopName
   )
   Write-Host "Initializing storage account $storageAccountName..." -ForegroundColor Yellow
   $tempFileName = [System.IO.Path]::GetTempFileName()
   New-Item -path $tempFileName -ItemType File -Force >$null
   az storage blob upload --account-name "$storageAccountName" --container-name "$loopName/exported-models" --file "$tempFileName" --name "current"
   if ($LASTEXITCODE -ne 0) {
      Write-Host "Failed to upload blob to Azure storage. If exported-models/current does not exist, rl_sim will not start successfully." -ForegroundColor Red
   }
   else {
      Write-Host "Storage initialized successfully." -ForegroundColor Green
   }
   Remove-Item -path $tempFileName -Force
}

###############################################################################
# main script
try {
   # verify the environment is ready to go
   Invoke-CmdWithLogging "Verifying Azure CLI status" {
      az account show *> $null
   } {
      throw "Please run 'az login --use-device-code' to authenticate with Azure before running this script"
   }

   $subscription = (az account show --query "name" -o tsv)
   Write-Host "`tUsing subscription name: $subscription"

   Invoke-CmdWithLogging "Verifying Azure Bicep status" {
      az bicep version *> $null
   } {
      throw "Please install Azure Bicep before running this script"
   }

   Invoke-CmdWithLogging "Verifying Docker Engine status" {
      docker info *> $null
   } {
      throw "Please start the Docker Engine before running this script"
   }

   # A future implementation will not include this method as the docker image will come from
   # a public Microsoft Container Registry and/or DockerHub.  Much or all of this script will be
   # be obsolete when that happens.
   $dockerImageTar = Invoke-CmdWithLogging "Perparing the docker image from $dockerImageFile" {
      $dockerPackage = Get-DockerImageTar -dockerImageFile $dockerImageFile
      Write-Host "Found existing docker image package: $dockerPackage" -ForegroundColor Yellow
      $dockerImagePackageUrl = Get-LatestDockerImageUrl
      if ($dockerImagePackageUrl) {
         Write-Host "Download the latest docker image package from (an authenticated session is needed): " -ForegroundColor White -NoNewline
         Write-Host $dockerImagePackageUrl -ForegroundColor Yellow
      }
      return $dockerPackage
   }

   # get the user object id to use to add the user roles in the deployment
   $userObjectId = Invoke-CmdWithLogging "Retrieving the Azure User Object Id" {
      return $(az ad signed-in-user show --query 'id' --output tsv)
   } {
      throw "Failed to retrieve the Azure User Object Id"
   }

   # determine if a keyvault or acr is needed for the image repository
   # for now, we only support acr with managed identity and dockerhub
   # with a keyvault to managed secrets
   $imageRepoKind = Get-ImageReposiotryKind -bicepParamFile $environmentParamsFile

   # deploy the environment resources
   $deployEnvProperties = Invoke-CmdWithLogging "Deploying the environment with params $environmentParamsFile" {
      $imageRepoUsername = $null
      $imageRepoPassword = $null
      if ($imageRepoKind -eq "dockerhub") {
         Write-Host ""
         $imageRepoUsername = Get-SecureString -Prompt "Enter the username for the image registry"
         $imageRepoPassword = Get-SecureString -Prompt "Enter the password for the image registry"
      }
      $deployEnvProperties = az deployment sub create `
         --location $location `
         --name "learning-loop-deploy-environment-$location" `
         --parameters ${environmentParamsFile} `
         --parameters userObjectIdOverride="$userObjectId" `
         --parameters imageRegistryUsername="$imageRepoUsername" `
         --parameters imageRegistryPassword="$imageRepoPassword" `
         --query 'properties' `
         --output json | ConvertFrom-Json
      $imageRepoUsername = $null
      $imageRepoPassword = $null
      if ($deployEnvProperties.provisioningState -ne "Succeeded") {
         throw "Deployment failed: ${deployEnvProperties.error.message}"
      }
      $mainDeployParams = $deployEnvProperties.outputs.loopDeploymentParams.value
      Out-File -FilePath $mainDeployParamsFile -InputObject $mainDeployParams -Encoding ascii
      return $deployEnvProperties
   } {
      throw "Failed to execute the deployment script with params $environmentParamsFile"
   }

   # Prepare the image for deployment
   #  - Retrieve the image host from the deployment environment properties.
   #     - If the image repository kind is "acr", logs into the Azure Container Registry (ACR).
   #     - If the image repository kind is "dockerhub", Docker Hub should be logged in already.
   #  - Load the Docker image from the specified tar file.
   #  - Retrieves the image name and tag from the deployment environment properties.
   #  - Tags the Docker image with the target tag name.
   #  - Pushes the Docker image to the specified image host.
   $imageHost = $deployEnvProperties.outputs.imageHost.value
   if ($imageRepoKind -eq "acr") {
      $acrName = $deployEnvProperties.outputs.acrName.value
      Invoke-CmdWithLogging "Logging into ACR $acrName" {
         az acr login --name "$acrName" *> $null
      } {
         throw "Failed to login to the Azure Container Registry '$acrName'"
      }
   }
   elseif ($imageRepoKind -ne "dockerhub") {
      throw "Unsupported image repository kind: $imageRepoKind"
   }

   Invoke-CmdWithLogging "Loading the docker image from $dockerImageTar" {
      docker load -i $dockerImageTar
   } {
      throw "Failed to load the Docker image from $dockerImageTar"
   }

   $imageName = $deployEnvProperties.outputs.imageName.value
   $imageTag = $deployEnvProperties.outputs.imageTag.value
   $targetTagName = "$imageHost/${imageName}:$imageTag"
   Invoke-CmdWithLogging "Tagging the docker image as $targetTagName" {
      docker tag learning-loop:latest $targetTagName
   } {
      throw "Failed to tag the Docker image as '$targetTagName'"
   }

   Invoke-CmdWithLogging "Pushing docker image $targetTagName" {
      docker push $targetTagName
   } {
      throw "Failed to push the Docker image to '$imageHost'"
   }

   # deploy the Leaning Loop application, storage, and event hub resources
   $deployMainProperties = Invoke-CmdWithLogging "Deploying the application with params $mainDeployParamsFile" {
      $deployMainProperties = az deployment group create --resource-group $deployEnvProperties.outputs.resourceGroupName.value --name "learning-loop-deploy-app-$location" --parameters $mainDeployParamsFile --query 'properties' --output json | ConvertFrom-Json
      if ($deployMainProperties.provisioningState -ne "Succeeded") {
         throw "Deployment failed: ${deployMainProperties.error.message}"
      }
      return $deployMainProperties
   } {
      throw "Deployment failed: ${deployMainProperties.error.message}"
   }

   # save the RL simulation configuration generated from the main deployment
   Write-Host "Saving the RL simulation configuration to $rlSimConfigFile" -ForegroundColor Yellow
   $rlSimConfig = $deployMainProperties.outputs.rlSimConfigAz.value
   if ($rlSimConfigType -eq "connstr") {
      $rlSimConfig = $deployMainProperties.outputs.rlSimConfigConnString.value
   }
   Out-File -FilePath $rlSimConfigFile -InputObject $rlSimConfig -Encoding ascii

   Write-Host "Deployment completed successfully" -ForegroundColor Green

   if ($initStorage) {
      # try to initialize the storage account to allow rl_sim to start
      Write-Host
      TryInitializeStorage -StorageAccountName $deployMainProperties.outputs.storageAccountName.value -LoopName $deployEnvProperties.outputs.loopName.value
   }

   Write-Host 
   Write-Host "The RL simulation configuration file is saved at $rlSimConfigFile" -ForegroundColor Yellow
   Write-Host "rl_sim usage:" -ForegroundColor Yellow
   Write-Host "`trl_sim_cpp -j $rlSimConfigFile"
   if ($rlSimConfigType -eq "connstr") {
      Write-Host "The RL simulation configuration is set to `"connstr`". You must update $rlSimConfigFile with the connection string before running rl_sim."
   }
   if ($deployMainProperties.outputs.rlSimContainerDeployed.value -eq "true") {
      Write-Host ""
      Write-Host "The RL simulation container is deployed. You many need to start the container instance to begin the simulation." -ForegroundColor Green
   }
}
catch {
   Write-Host ""
   Write-Error "An error occurred on line $($_.InvocationInfo.ScriptLineNumber):"
   Write-Error $_.Exception.Message
   exit 1
}
