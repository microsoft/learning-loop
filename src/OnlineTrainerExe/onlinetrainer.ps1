# FILE: onlinetrainer.ps1

Write-Output "Calling OnlineTrainer trainer from PowerShell script"
dotnet Microsoft.DecisionService.OnlineTrainer.dll $args
$exitstatus = $LASTEXITCODE
Start-Sleep -Seconds 60 # sleep for some time to let the core file be created.
exit $exitstatus