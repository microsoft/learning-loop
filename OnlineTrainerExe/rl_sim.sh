#!/bin/bash
if [ -z "$RL_SIM_CONFIG" ]; then
  echo "RL_SIM_CONFIG environment variable is not set."
  exit 1
fi

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

echo "Detected rl_sim for $OS: $EXECUTABLE"
# Check if the executable exists and run it
if [ -f "$EXECUTABLE" ]; then
  $EXECUTABLE -j rl_sim_config.json
else
  echo "Executable not found: $EXECUTABLE"
  exit 1
fi
