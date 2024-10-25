#!/bin/bash

LOG_FILE="./rl_sim_start.log"
echo_log() {
  echo "$(date '+%Y-%m-%d %H:%M:%S') - $1" | tee -a "$LOG_FILE"
}

if [ -z "$RL_SIM_CONFIG" ]; then
  echo_log "ERROR: RL_SIM_CONFIG environment variable is not set."
  exit 1
fi

echo_log "INFO: Created rl_sim_config.json with the following content: $RL_SIM_CONFIG"
echo "$RL_SIM_CONFIG" >rl_sim_config.json

# Define the base path for the executable
BASE_PATH="./rl_sim"

# Determine the operating system
OS=$(uname -s)

# Determine the appropriate executable based on the operating system
case "${OS}" in
  Linux*)
    EXECUTABLE="$BASE_PATH/rl_sim-linux-x64"
    ;;
  Darwin*)
    EXECUTABLE="$BASE_PATH/rl_sim-macos-x64"
    ;;
  CYGWIN* | MINGW* | MSYS*)
    EXECUTABLE="$BASE_PATH/rl_sim-win-x64.exe"
    ;;
  *)
    echo "Unsupported OS: ${OS}"
    exit 1
    ;;
esac

# Variables
CONTAINER_NAME="${LEARNING_LOOP_NAME}/exported-models"
MODEL_NAME="current"
FILE_PATH="./empty_model"

# Check if the blob already exists
EXISTING_MODEL=$(az storage blob exists \
  --account-name "$STORAGE_ACCOUNT_NAME" \
  --container-name "$CONTAINER_NAME" \
  --name "$MODEL_NAME" \
  --query "exists" \
  --output tsv 2>> "$LOG_FILE")

if [ $? -ne 0 ]; then
  echo_log "ERROR: Failed to check if the blob exists. Please verify your Azure CLI setup and credentials."
  exit 1
fi

if [ -z "$EXISTING_MODEL" ]; then
  echo_log "ERROR: Unable to verify if there is an existing exported model. rl_sim may not start successfully without an exported model."
  exit 1
elif [ "$EXISTING_MODEL" == "false" ]; then
  echo_log "INFO: An exported model does not exist in the container '$CONTAINER_NAME'. Attempting to upload an empty model..."
  echo '' > "$FILE_PATH"
  
  # Upload the empty model to Azure Blob Storage
  az storage blob upload \
    --account-name "$STORAGE_ACCOUNT_NAME" \
    --container-name "$CONTAINER_NAME" \
    --name "$MODEL_NAME" \
    --file "$FILE_PATH" 2>> "$LOG_FILE"
  
  if [ $? -eq 0 ]; then
    echo_log "INFO: Uploaded '$FILE_PATH' successfully to container '$CONTAINER_NAME' as '$MODEL_NAME'."
  else
    echo_log "ERROR: Failed to upload file '$FILE_PATH'. rl_sim may not start successfully without an exported model."
  fi
  
  # Clean up
  rm "$FILE_PATH"
  echo_log "INFO: Cleaned up temporary file '$FILE_PATH'."
else
  echo_log "INFO: Blob '$MODEL_NAME' already exists in container '$CONTAINER_NAME'. No upload needed."
fi

echo_log "INFO: Detected rl_sim for $OS: $EXECUTABLE"
# Check if the executable exists and run it
if [ -f "$EXECUTABLE" ]; then
  $EXECUTABLE -j rl_sim_config.json
else
  echo_log "ERROR: Executable not found: $EXECUTABLE"
  exit 1
fi
