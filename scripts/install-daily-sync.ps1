<#
.SYNOPSIS
    Installs a daily KBC sync that runs even when the web app is closed.

.DESCRIPTION
    Publishes the web host (src/Containers/UI/Hosting/KbcTracker.Web) into %LOCALAPPDATA%\KbcTracker\app and
    registers the Windows scheduled task "KbcTracker daily sync", which runs "KbcTracker.Web.dll --sync" once a day.
    The transaction history then keeps growing without opening the app.

    Side effects: deletes and rebuilds the published app in %LOCALAPPDATA%\KbcTracker\app (binaries only; your data
    in %LOCALAPPDATA%\KbcTracker is not touched) and creates or replaces the scheduled task for the current user.
    Run it again after updating the app, because the task keeps using the build it was installed with. Use -WhatIf
    to see what would happen without changing anything.

.PARAMETER Time
    The time of day of the sync, as HH:mm (24-hour clock). Defaults to 12:40.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\install-daily-sync.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\install-daily-sync.ps1 -Time 07:30 -WhatIf

.NOTES
    Remove the task with: Unregister-ScheduledTask -TaskName "KbcTracker daily sync" -Confirm:$false
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidatePattern('^([01]\d|2[0-3]):[0-5]\d$')]
    [string]$Time = '12:40'
)

$ErrorActionPreference = 'Stop'

$taskName = 'KbcTracker daily sync'
$repository = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repository 'src/Containers/UI/Hosting/KbcTracker.Web/KbcTracker.Web.csproj'
$appFolder = Join-Path $env:LOCALAPPDATA 'KbcTracker\app'
$appDll = Join-Path $appFolder 'KbcTracker.Web.dll'

if ($PSCmdlet.ShouldProcess($appFolder, "Replace the published app with a new build of $project")) {
    # The app folder holds only the published binaries (the data lives in its parent folder). Clearing it first
    # removes files of older builds, so a stale program can never be started from it.
    if (Test-Path -LiteralPath $appFolder) {
        Remove-Item -LiteralPath $appFolder -Recurse -Force
    }

    Write-Host "Building into $appFolder ..."
    # Windows PowerShell turns any stderr line of a native command into a terminating error under 'Stop', even when
    # the command succeeds; the exit code below decides instead.
    $output = & {
        $ErrorActionPreference = 'Continue'
        & dotnet publish $project -c Release -o $appFolder 2>&1
    }
    if ($LASTEXITCODE -ne 0) {
        $output | ForEach-Object { Write-Error -Message "$_" -ErrorAction Continue }
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }
}

if ($PSCmdlet.ShouldProcess($taskName, "Register a daily sync at $Time")) {
    if (-not (Test-Path -LiteralPath $appDll)) {
        throw "$appDll does not exist; publish the app first (run the script again without declining the publish step)."
    }

    $action = New-ScheduledTaskAction -Execute 'dotnet' -Argument "`"$appDll`" --sync" -WorkingDirectory $appFolder
    $trigger = New-ScheduledTaskTrigger -Daily -At $Time
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null

    Write-Host "Done. KBC transactions will be synced every day at $Time (or as soon as the PC is on)."
    Write-Host "Test it now:  Push-Location `"$appFolder`"; dotnet .\KbcTracker.Web.dll --sync; Pop-Location"
}
