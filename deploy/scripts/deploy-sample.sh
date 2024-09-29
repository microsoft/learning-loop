#!/usr/bin/env bash
SCRIPT_DIR=$(dirname "$0")
pwsh -File "$SCRIPT_DIR/deploy-sample.ps1" "$@"