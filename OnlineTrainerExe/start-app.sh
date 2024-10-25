#!/bin/bash
START_CMD="onlinetrainer.sh"
if [[ -n "$RL_START_WITH" && -f "$RL_START_WITH" && -x "$RL_START_WITH" ]]; then
  START_CMD=$RL_START_WITH
fi

echo "Starting up with: $RL_START_WITH"
./"$START_CMD"
