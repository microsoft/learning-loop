#!/bin/bash
###############################################################################
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
###############################################################################
# Synpopsis:
# Deploys a sample Learning Loop environment.
#
# Description:
# This script deploys a sample Learning Loop environment using specified parameters for location,
# environment parameters file, main deployment parameters file, Docker image file, RL simulation
# configuration type, RL simulation configuration file, and the expected archived image name.
#
# A successful deployment will create/overwrite a file named "main-deploy.bicepparam" (default) in the current directory,
# and a RL simulation configuration file "rl-sim-config.json" (default) in the current directory.
#
# Prior to running this script, you must have the following:
#
#    1) The Azure CLI and the Azure Bicep tools installed and in the path.  An current Azure CLI
#       will include Azure Bicep.
#    2) You must be logged in to the Azure CLI with a user account that has the required permissions to
#       create a resource group, storage account, event hub, application insights, and a managed identity.
#    3) Docker Engine installed and running.
#    4) The Leaning Loop docker image file avialable on the filesystem.

set -e # exit immediately if a command exits with a non-zero status

# define color codes
GREEN='\e[32m'
RED='\e[31m'
YELLOW='\e[33m'
NC='\e[0m'  # No Color

# default parameters
location="westus2"
environmentParamsFile="./sample-loop-environment.acr.bicepparam"
mainDeployParamsFile="./main-deploy.bicepparam"
dockerImageFile="./docker-image-ubuntu-latest.zip"
rlSimConfigType="az"
rlSimConfigFile="./rl-sim-config.json"
expectedArchivedImageName="./learning-loop-ubuntu-latest"

print_usage() {
   echo "Usage: $0 [options]"
   echo "This script deploys a sample Learning Loop environment."
   echo
   echo "Options:"
   echo "  --location <location>                The Azure region to deploy the resources (default: $location)"
   echo "  --environmentParamsFile <file>       The Bicep parameter file for the environment deployment (default: $environmentParamsFile)"
   echo "  --mainDeployParamsFile <file>        The Bicep parameter file for the generated main deployment (default: $mainDeployParamsFile)"
   echo "  --dockerImageFile <file>             The Docker image file to deploy (default: $dockerImageFile)"
   echo "  --rlSimConfigType <type>             The type of generated RL simulation configuration (az or connstr) (default: $rlSimConfigType)"
   echo "  --rlSimConfigFile <file>             The file to save the RL simulation configuration (default: $rlSimConfigFile)"
   echo "  --expectedArchivedImageName <name>   The expected name of the archived Docker image (default: $expectedArchivedImageName)"
   echo
   echo "A successful deployment will create/overwrite a file named 'main-deploy.bicepparam' (default) in the current directory,"
   echo "and a RL simulation configuration file 'rl-sim-config.json' (default) in the current directory."
   echo
   echo "Prior to running this script, you must have the following:"
   echo
   echo "   1) The Azure CLI and the Azure Bicep tools installed and in the path.  An current Azure CLI"
   echo "      will include Azure Bicep."
   echo "   2) You must be logged in to the Azure CLI with a user account that has the required permissions to"
   echo "      create a resource group, storage account, event hub, application insights, and a managed identity."
   echo "   3) Docker Engine installed and running."
   echo "   4) The Leaning Loop docker image file avialable on the filesystem."
}

# capture the command line arguments and set the corresponding variables
parse_arguments() {
   while [[ "$#" -gt 0 ]]; do
      case $1 in
      --location)
         location="$2"
         shift
         ;;
      --environmentParamsFile)
         environmentParamsFile="$2"
         shift
         ;;
      --mainDeployParamsFile)
         mainDeployParamsFile="$2"
         shift
         ;;
      --dockerImageFile)
         dockerImageFile="$2"
         shift
         ;;
      --rlSimConfigType)
         rlSimConfigType="$2"
         shift
         ;;
      --rlSimConfigFile)
         rlSimConfigFile="$2"
         shift
         ;;
      --expectedArchivedImageName)
         expectedArchivedImageName="$2"
         shift
         ;;
      --help)
         print_usage
         exit 0
         ;;
      *)
         echo -e "${RED}Unknown parameter passed: $1${NC}"
         echo
         print_usage
         exit 1
         ;;
      esac
      shift
   done
}

# helper to check if a parameter is empty. if the parameter is empty,
# print an error message and bail
fail_if_empty() {
  local param_value="$1"
  local param_name="$2"

  if [ -z "$param_value" ]; then
    echo -e "${RED}The $param_name parameter is required${NC}" >&2
    print_usage
    exit 1
  fi
}

# validate command line parameter values; bail if validation fails.
validate_arguments() {
   fail_if_empty "$location" "location"
   fail_if_empty "$environmentParamsFile" "environmentParamsFile"
   fail_if_empty "$mainDeployParamsFile" "mainDeployParamsFile"
   fail_if_empty "$dockerImageFile" "dockerImageFile"
   fail_if_empty "$rlSimConfigType" "rlSimConfigType"
   fail_if_empty "$rlSimConfigFile" "rlSimConfigFile"
   fail_if_empty "$expectedArchivedImageName" "expectedArchivedImageName"

   if [ ! -f "$environmentParamsFile" ]; then
      echo -e "${RED}Cannot find the environment parameters file $environmentParamsFile${NC}" >&2
      exit 1
   fi

   if [ ! -f "$dockerImageFile" ]; then
      echo -e "${RED}Cannot find the docker image file $dockerImageFile${NC}" >&2
      exit 1
   fi

   if [ "$rlSimConfigType" != "az" ] && [ "$rlSimConfigType" != "connstr" ]; then
      echo -e "${RED}Unsupported RL simulation configuration type: $rlSimConfigType${NC}" >&2
      exit 1
   fi
}

# expand a gzip file; wrapped to insulate for possible changes
expand_gzip() {
   local gzipFile="$1"
   local destination="$2"
   gunzip -c "$gzipFile" >"$destination"
}

# get the latest Docker image URL from the GitHub Actions workflow
get_latest_docker_image_url() {
   local owner="microsoft"
   local repo="learning-loop"
   local workflowName="build_all.yml"
   local artifactNameFilter="docker-image-ubuntu-latest"
   local apiUrl="https://api.github.com/repos/$owner/$repo/actions/workflows/$workflowName/runs?status=success&per_page=1"
   local headers="User-Agent: bash"

   local workflowRuns=$(curl -s -H "$headers" "$apiUrl")
   local latestRunId=$(echo "$workflowRuns" | jq -r '.workflow_runs[0].id')

   local artifactUrl="https://api.github.com/repos/$owner/$repo/actions/runs/$latestRunId/artifacts"
   local artifacts=$(curl -s -H "$headers" "$artifactUrl")
   local artifact=$(echo "$artifacts" | jq -r ".artifacts[] | select(.name == \"$artifactNameFilter\") | .id")

   if [ -n "$artifact" ]; then
      echo "https://github.com/$owner/$repo/actions/runs/$latestRunId/artifacts/$artifact"
   else
      echo ""
   fi
}

# extract the the Docker image tar file from an archive file
get_docker_image_tar() {
   local dockerImageFile="$1"
   if [ ! -f "$dockerImageFile" ]; then
      local imagePackageUrl=$(get_latest_docker_image_url)
      echo -e "${RED}Cannot find docker image file $dockerImageFile. Download the docker image file from the GitHub Actions artifacts page ($imagePackageUrl).${NC}" >&2
      exit 1
   fi

   local imageFileType="${dockerImageFile##*.}"
   case "$imageFileType" in
   tar)
      echo "$dockerImageFile"
      ;;
   zip)
      unzip -o "$dockerImageFile" -d . > /dev/null
      local expectedGzFile="${expectedArchivedImageName}.tar.gz"
      if [ ! -f "$expectedGzFile" ]; then
         echo -e "${RED}Cannot find the gzip'd docker image file $expectedGzFile unpacked from $dockerImageFile${NC}" >&2
         exit 1
      fi
      local dockerImageTar="${expectedGzFile%.gz}"
      expand_gzip "$expectedGzFile" "$dockerImageTar"
      if [ ! -f "$dockerImageTar" ]; then
         echo -e "${RED}Cannot find the tar'd docker image file $dockerImageTar unpacked from $expectedGzFile${NC}" >&2
         exit 1
      fi
      echo "$dockerImageTar"
      ;;
   gz)
      local dockerImageTar="${dockerImageFile%.gz}"
      expand_gzip "$dockerImageFile" "$dockerImageTar"
      if [ ! -f "$dockerImageTar" ]; then
         echo -e "${RED}Cannot find the tar'd docker image file $dockerImageTar unpacked from $dockerImageFile${NC}" >&2
         exit 1
      fi
      echo "$dockerImageTar"
      ;;
   *)
      echo -e "${RED}Unsupported image file type: $imageFileType${NC}" >&2
      exit 1
      ;;
   esac
}

# get image repository kind from a bicep parameter file
get_image_repository_kind() {
   local bicepParamFile="$1"
   kind_value=$(awk '
   BEGIN { inside_image_block = 0; inside_properties_block = 0; properties_level = 0; kind_value = "" }

   /[[:space:]]*image:/ { inside_image_block = 1 }  # Start tracking when image block is found
   {
      # Track braces only inside the image block
      if (inside_image_block && /{/ ) { properties_level++ }  # Increase level when entering a block
      if (inside_image_block && /}/ ) { properties_level-- }  # Decrease level when exiting a block

      # Enter the properties block at the first level
      if (inside_image_block && /properties:/) { 
         inside_properties_block = 1
         properties_level = 1  # Set level to 1 when entering properties
      }

      # Only look for 'kind' at the first level of the properties block
      if (inside_properties_block && properties_level == 1 && /kind:\s*'\''[^'\'']*'\''/) {
         match($0, /kind:\s*'\''[^'\'']*'\''/, arr)
         split(arr[0], kv, "'\''")
         kind_value = kv[2]
      }

      # Exit properties block when closing the first-level braces
      if (inside_properties_block && properties_level == 0) {
         inside_properties_block = 0
      }

      # Exit image block when closing all braces
      if (inside_image_block && properties_level == 0) {
         inside_image_block = 0
         inside_properties_block = 0
      }
   }
   END { if (kind_value != "") print kind_value }
   ' "$bicepParamFile")

   # Check if the kind value was found
   if [ -z "$kind_value" ]; then
      echo -e "${RED}Cannot find the image repository kind in the image properties section of the bicep parameter file $bicepParamFile${NC}" >&2
      exit 1
   else
      echo "$kind_value"
   fi
}

# invoke command with logging
invoke_cmd_with_logging() {
   local message="$1"
   local command="$2"
   local errorHandler="$3"

   echo -ne "${YELLOW}$message... ${NC}" >&2
   
   # Capture both the output and exit code of the command
   local output
   set +e
   output=$(eval "$command")  # Capture both stdout and stderr
   local exitCode=$?
   set -e
   
   if [ $exitCode -ne 0 ]; then
      if [ -n "$errorHandler" ]; then
         eval "$errorHandler"
      fi
      echo -e "${RED}Failed${NC}" >&2
      echo -e "${RED}$output${NC}" >&2  # Output the error message
      return $exitCode
   fi

   echo -e "${GREEN}Success${NC}" >&2
   echo "$output"  # Return the output of the command
   return 0
}

# initialize the storage account
initialize_storage_account() {
   local storageAccountName=$1
   local loopName=$2
   echo -e "${YELLOW}Initializing storage account $storageAccountName...${NC}"
   tempFile=$(mktemp)
   trap "rm -f '$tempFile'" EXIT
   if az storage blob upload \
      --account-name "$storageAccountName" \
      --container-name "$loopName/exported-models" \
      --file "$tempFile" \
      --name "current" > /dev/null; then
      echo -e "${GREEN}Storage initialized successfully.${NC}"
   else
      echo -e "${RED}Error: Failed to upload blob to Azure storage. If exported-models/current does not exist, rl_sim will not start successfully.${NC}"
   fi
   rm "$tempFile"
   trap - EXIT   
}

###############################################################################
# main script
main() {
   trap 'echo "An error occurred on line $LINENO: $BASH_COMMAND"; exit 1' ERR

   # verify the environment is ready to go
   invoke_cmd_with_logging "Verifying Azure CLI status" \
      "az account show > /dev/null" \
      "echo 'Please run \"az login --use-device-code\" to authenticate with Azure before running this script' >&2; exit 1"

   invoke_cmd_with_logging "Verifying Azure Bicep status" \
      "az bicep version > /dev/null" \
      "echo 'Please install Azure Bicep before running this script' >&2; exit 1"

   invoke_cmd_with_logging "Verifying Docker Engine status" \
      "docker info > /dev/null" \
      "echo 'Please start the Docker Engine before running this script' >&2; exit 1"

   # A future implementation will not include this method as the docker image will come from
   # a public Microsoft Container Registry and/or DockerHub.  Much or all of this script will be
   # be obsolete when that happens.
   dockerImageTar=$(invoke_cmd_with_logging "Preparing the docker image from $dockerImageFile" \
      "get_docker_image_tar \"$dockerImageFile\"" \
      "")

   # get the user object id to use to add the user roles in the deployment
   userObjectId=$(invoke_cmd_with_logging "Retrieving the Azure User Object Id" \
      "az ad signed-in-user show --query 'id' --output tsv" \
      "echo 'Failed to retrieve the Azure User Object Id' >&2; exit 1")
   if [ -z "$userObjectId" ]; then
      echo "Failed to retrieve the Azure User Object Id" >&2
      exit 1
   fi

   # determine if a keyvault or acr is needed for the image repository
   # for now, we only support acr with managed identity and dockerhub
   # with a keyvault to managed secrets
   imageRepoKind=$(get_image_repository_kind "$environmentParamsFile")
   imageRepoUsername=''
   imageRepoPassword=''
   if [ "$imageRepoKind" == 'dockerhub' ]; then
      read -sp 'Enter the username for the image registry: ' imageRepoUsername
      echo
      read -sp 'Enter the password for the image registry: ' imageRepoPassword
      echo
   fi;

   # deploy the environment resources
   deployEnvProperties=$(invoke_cmd_with_logging "Deploying the environment with params $environmentParamsFile" \
      "az deployment sub create \
         --location \"$location\" \
         --name 'learning-loop-deploy-environment' \
         --parameters \"$environmentParamsFile\" \
         --parameters userObjectIdOverride=\"$userObjectId\" \
         --parameters imageRegistryUsername=\"$imageRepoUsername\" \
         --parameters imageRegistryPassword=\"$imageRepoPassword\" \
         --query 'properties' \
         --output json | jq '.'" \
      "echo 'Failed to execute the deployment script with params $environmentParamsFile' >&2; exit 1")
   deployStatus=$(echo "$deployEnvProperties" | jq -r '.provisioningState')
   if [ "$deployStatus" == "Succeeded" ]; then
      echo -e "${YELLOW}Saving deployment parameters to $mainDeployParamsFile${NC}"
      loopDeploymentParams=$(echo "$deployEnvProperties" | jq -r '.outputs.loopDeploymentParams.value')
      echo "$loopDeploymentParams" > "$mainDeployParamsFile"
   else
      echo -e "${RED}Deployment failed with status $deployStatus${NC}" >&2
      exit 1
   fi

   # Prepare the image for deployment
   #  - Retrieve the image host from the deployment environment properties.
   #     - If the image repository kind is "acr", logs into the Azure Container Registry (ACR).
   #     - If the image repository kind is "dockerhub", Docker Hub should be logged in already.
   #  - Load the Docker image from the specified tar file.
   #  - Retrieves the image name and tag from the deployment environment properties.
   #  - Tags the Docker image with the target tag name.
   #  - Pushes the Docker image to the specified image host.
   imageHost=$(echo "$deployEnvProperties" | jq -r '.outputs.imageHost.value')
   if [ "$imageRepoKind" == "acr" ]; then
      acrName=$(echo "$deployEnvProperties" | jq -r '.outputs.acrName.value')
      invoke_cmd_with_logging "Logging into ACR $acrName" \
         "az acr login --name \"$acrName\" > /dev/null" \
         "echo 'Failed to login to the Azure Container Registry $acrName' >&2; exit 1"
   elif [ "$imageRepoKind" != "dockerhub" ]; then
      echo -e "${RED}Unsupported image repository kind: $imageRepoKind${NC}" >&2
      exit 1
   fi

   invoke_cmd_with_logging "Loading the docker image from $dockerImageTar" \
      "docker load -i \"$dockerImageTar\"" \
      "echo 'Failed to load the Docker image from $dockerImageTar' >&2; exit 1"

   imageName=$(echo "$deployEnvProperties" | jq -r '.outputs.imageName.value')
   imageTag=$(echo "$deployEnvProperties" | jq -r '.outputs.imageTag.value')
   targetTagName="$imageHost/${imageName}:$imageTag"
   invoke_cmd_with_logging "Tagging the docker image as $targetTagName" \
      "docker tag learning-loop:latest \"$targetTagName\"" \
      "echo 'Failed to tag the Docker image as $targetTagName' >&2; exit 1"

   invoke_cmd_with_logging "Pushing docker image $targetTagName" \
      "docker push \"$targetTagName\"" \
      "echo 'Failed to push the Docker image to $imageHost' >&2; exit 1"

   # deploy the Leaning Loop application, storage, and event hub resources
   resourceGroupName=$(echo "$deployEnvProperties" | jq -r '.outputs.resourceGroupName.value')
   deployMainProperties=$(invoke_cmd_with_logging "Deploying the application with params $mainDeployParamsFile" \
      "az deployment group create \
         --resource-group \"$resourceGroupName\" \
         --name 'learning-loop-deploy-app' \
         --parameters \"$mainDeployParamsFile\" \
         --query 'properties' \
         --output json | jq '.'" \
      "echo 'Deployment failed: \$(echo \"$deployMainProperties\" | jq -r '.error.message')' >&2; exit 1")
   deployStatus=$(echo "$deployMainProperties" | jq -r '.provisioningState')
   if [ "$deployStatus" != "Succeeded" ]; then
      echo -e "${RED}Deployment failed with status $deployStatus${NC}" >&2
      exit 1
   fi

   # save the RL simulation configuration generated from the main deployment
   rlSimConfig=$(echo "$deployMainProperties" | jq -r '.outputs.rlSimConfigAz.value')
   if [ "$rlSimConfigType" == "connstr" ]; then
      rlSimConfig=$(echo "$deployMainProperties" | jq -r '.outputs.rlSimConfigConnStr.value')
   fi
   echo -e "${YELLOW}Saving the RL simulation configuration to $rlSimConfigFile${NC}"
   rlSimConfig=$(echo "$deployMainProperties" | jq -r '.outputs.rlSimConfigAz.value')
   echo "$rlSimConfig" >"$rlSimConfigFile"

   echo -e "${GREEN}Deployment completed successfully${NC}"
   echo
   storageAccountName=$(echo "$deployMainProperties" | jq -r '.outputs.storageAccountName.value')
   loopName=$(echo "$deployEnvProperties" | jq -r '.outputs.loopName.value')

   # try to initialize the storage account to allow rl_sim to start
   initialize_storage_account "$storageAccountName" "$loopName"
   echo
   echo -e "${YELLOW}The RL simulation configuration file is saved at $rlSimConfigFile${NC}"
   echo "rl_sim usage:"
   echo -e "\trl_sim_cpp -j $rlSimConfigFile"
   if [ "$rlSimConfigType" == "connstr" ]; then
      echo "The RL simulation configuration is set to \"connstr\". You must update $rlSimConfigFile with the connection string before running rl_sim."
   fi
}

parse_arguments "$@"
validate_arguments
main "$@"
