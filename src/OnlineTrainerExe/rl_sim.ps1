# FILE: rl_sim.ps1

$LOG_FILE = "./rl_sim_start.log"

function echo_log {
    param (
        [string]$message
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "$timestamp - $message"
    $logMessage | Tee-Object -FilePath $LOG_FILE -Append
}

if (-not $env:RL_SIM_CONFIG) {
    echo_log "ERROR: RL_SIM_CONFIG environment variable is not set."
    Start-Sleep -Seconds ([System.Int32]::MaxValue)
}

echo_log "INFO: Created rl_sim_config.json with the following content: $env:RL_SIM_CONFIG"
$env:RL_SIM_CONFIG | Out-File -FilePath "rl_sim_config.json"

# Define the base path for the executable
$BASE_PATH = "./rl-sim"

# Determine the appropriate executable for Windows
$EXECUTABLE = "$BASE_PATH/rl_sim-win-x64.exe"
echo_log "INFO: Detected rl_sim for Windows: $EXECUTABLE"

$CONTAINER_NAME = "$env:LEARNING_LOOP_NAME/exported-models"
$MODEL_NAME = "current"
$FILE_PATH = "./empty_model"

echo_log "INFO: Logging into Azure..."
az login --identity
if ($LASTEXITCODE -ne 0) {
    echo_log "ERROR: Failed to log into Azure. This may be due to delayed role assignment. The container will terminate and restart in 60 seconds."
    Start-Sleep -Seconds 60
    echo_log "INFO: EXIT"
    exit 1
}

echo_log "INFO: Checking for an existing exported-model..."
$EXISTING_MODEL = az storage blob exists `
    --auth-mode login `
    --account-name $env:STORAGE_ACCOUNT_NAME `
    --container-name $CONTAINER_NAME `
    --name $MODEL_NAME `
    --query "exists" `
    --output tsv 2>> $LOG_FILE

if (-not $EXISTING_MODEL) {
    echo_log "ERROR: Unable to verify if there is an existing exported model. rl_sim may not start successfully without an existing exported model."
} elseif ($EXISTING_MODEL -eq "false") {
    New-Item -Path $FILE_PATH -ItemType File -Force | Out-Null

    echo_log "INFO: An exported model does not exist in the container '$CONTAINER_NAME'. Attempting to upload an empty model..."
    az storage blob upload `
        --auth-mode login `
        --account-name $env:STORAGE_ACCOUNT_NAME `
        --container-name $CONTAINER_NAME `
        --name $MODEL_NAME `
        --file $FILE_PATH 2>> $LOG_FILE

    if ($LASTEXITCODE -eq 0) {
        echo_log "INFO: Uploaded '$FILE_PATH' successfully to container '$CONTAINER_NAME' as '$MODEL_NAME'."
    } else {
        echo_log "ERROR: Failed to upload file '$FILE_PATH'. rl_sim may not start successfully without an existing exported model."
    }

    echo_log "INFO: Cleaning up temporary file '$FILE_PATH'."
    Remove-Item -Path $FILE_PATH -Force
} else {
    echo_log "INFO: Blob '$MODEL_NAME' already exists in container '$CONTAINER_NAME'. No upload needed."
}

echo_log "INFO: Starting simulation..."
if (Test-Path -Path $EXECUTABLE) {
    & $EXECUTABLE -j rl_sim_config.json $env:RL_SIM_ARGS
} else {
    echo_log "ERROR: Executable not found: $EXECUTABLE"
}
echo_log "INFO: Simulation has ended."
Start-Sleep -Seconds ([System.Int32]::MaxValue)