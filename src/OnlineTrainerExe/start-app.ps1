#!/usr/bin/env pwsh
$START_CMD = "onlinetrainer.ps1"

if ($env:RL_START_WITH -and (Test-Path $env:RL_START_WITH) -and (Get-Command $env:RL_START_WITH -ErrorAction SilentlyContinue)) {
    $START_CMD = $env:RL_START_WITH
}

Write-Output "Starting up with: $START_CMD"
& "./$START_CMD"