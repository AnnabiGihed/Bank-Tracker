<#
  Builds KbcTracker into %LOCALAPPDATA%\KbcTracker\app and registers a Windows scheduled task
  that syncs your KBC transactions every day, even when the app is closed.
  Your history then keeps growing without you having to open the app.

  Run once from the repository folder:   powershell -ExecutionPolicy Bypass -File .\scripts\install-daily-sync.ps1
  Remove it:                             Unregister-ScheduledTask -TaskName "KbcTracker daily sync" -Confirm:$false
#>
param([string]$Time = "12:40")

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$app  = Join-Path $env:LOCALAPPDATA "KbcTracker\app"

Write-Host "Building into $app ..."
dotnet publish "$repo\KbcTracker.csproj" -c Release -o $app | Out-Null

$action  = New-ScheduledTaskAction -Execute "dotnet" -Argument "`"$app\KbcTracker.dll`" --sync" -WorkingDirectory $app
$trigger = New-ScheduledTaskTrigger -Daily -At $Time
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName "KbcTracker daily sync" -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null

Write-Host "Done. KBC transactions will be synced every day at $Time (or as soon as the PC is on)."
Write-Host "Test it now:  dotnet `"$app\KbcTracker.dll`" --sync"
