#!/bin/bash
LOG_FILE="./rl_sim_start.log"
echo_log() {
   echo "$(date '+%Y-%m-%d %H:%M:%S') - $1" | tee -a "$LOG_FILE"
}

if [ -z "$RL_SIM_CONFIG" ]; then
   echo_log "ERROR: RL_SIM_CONFIG environment variable is not set."
   sleep infinity
fi

echo_log "INFO: Created rl_sim_config.json with the following content: $RL_SIM_CONFIG"
echo "$RL_SIM_CONFIG" >rl_sim_config.json

# Define the base path for the executable
BASE_PATH="./rl-sim"

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
   echo_log "ERROR: Unsupported OS - ${OS}"
   sleep infinity
   ;;
esac
echo_log "INFO: Detected rl_sim for $OS: $EXECUTABLE"

set +e
CONTAINER_NAME="${LEARNING_LOOP_NAME}/exported-models"
MODEL_NAME="current"
FILE_PATH="./empty_model"

echo_log "INFO: Logging into Azure..."
az login --identity
if [ $? -ne 0 ]; then
   echo_log "ERROR: Failed to log into Azure. This may be due to delayed role assignment. The container will terminate and restart in 60 seconds."
   sleep 60
   echo_log "INFO: EXIT"
   exit 1
fi

echo_log "INFO: Checking for an existing exported-model..."
EXISTING_MODEL=$(az storage blob exists \
   --auth-mode login \
   --account-name "$STORAGE_ACCOUNT_NAME" \
   --container-name "$CONTAINER_NAME" \
   --name "$MODEL_NAME" \
   --query "exists" \
   --output tsv 2>>"$LOG_FILE")

if [ -z "$EXISTING_MODEL" ]; then
   echo_log "ERROR: Unable to verify if there is an existing exported model. rl_sim may not start successfully without an existing  exported model."
elif [ "$EXISTING_MODEL" == "false" ]; then
   touch "$FILE_PATH"

   echo_log "INFO: An exported model does not exist in the container '$CONTAINER_NAME'. Attempting to upload an empty model..."
   az storage blob upload \
      --auth-mode login \
      --account-name "$STORAGE_ACCOUNT_NAME" \
      --container-name "$CONTAINER_NAME" \
      --name "$MODEL_NAME" \
      --file "$FILE_PATH" 2>>"$LOG_FILE"

   if [ $? -eq 0 ]; then
      echo_log "INFO: Uploaded '$FILE_PATH' successfully to container '$CONTAINER_NAME' as '$MODEL_NAME'."
   else
      echo_log "ERROR: Failed to upload file '$FILE_PATH'. rl_sim may not start successfully without an existing exported model."
   fi

   echo_log "INFO: Cleaning up temporary file '$FILE_PATH'."
   rm "$FILE_PATH"
else
   echo_log "INFO: Blob '$MODEL_NAME' already exists in container '$CONTAINER_NAME'. No upload needed."
fi
set -e

echo_log "INFO: Starting simulation..."
if [ -f "$EXECUTABLE" ]; then
   $EXECUTABLE -j rl_sim_config.json $RL_SIM_ARGS
else
   echo_log "ERROR: Executable not found: $EXECUTABLE"
fi
echo_log "INFO: Simulation has ended."
sleep infinity
