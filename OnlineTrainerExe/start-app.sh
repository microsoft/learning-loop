#!/bin/bash
if [[ -n "$RL_START_WITH" && -x "$RL_START_WITH" ]]; then
   echo "Starting up with: $RL_START_WITH"
  ./"$RL_START_WITH"
else
  ./onlinetrainer.sh
fi
