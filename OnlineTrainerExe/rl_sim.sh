#!/bin/bash
if [ -z "$RL_SIM_CONFIG" ]; then
  echo "RL_SIM_CONFIG environment variable is not set."
  exit 1
fi

echo "$RL_SIM_CONFIG" > rl_sim_config.json

echo "Running RL_SIM..."
rl_sim/rl_sim-linux-x64 -j rl_sim_config.json