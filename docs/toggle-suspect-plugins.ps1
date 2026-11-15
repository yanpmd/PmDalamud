<#
.SYNOPSIS
    Temporarily disable/restore suspect plugins for crash triage (fully reversible).

.DESCRIPTION
    The 11/15 crash stacks still show third-party plugins on the global AtkUnitBase
    hook chain:
      - KamiToolKit.NativeAddon (old namespace) -> plugins packaged 2026-04..07 with an old KTK
      - OmenTools.OmenService.*                -> DailyRoutines / PilgrimsTraverse
      - SimpleTweaksPlugin.SimpleEvent.GlobalEventDetour

    This script only renames version directories under installedPlugins
    (<Plugin>\<Version> -> <Version>.off) so Dalamud cannot load that version.
    Run with -Restore to put everything back. No files are deleted.

    Close the game completely before running.

.EXAMPLE
    .\toggle-suspect-plugins.ps1 -Status
    .\toggle-suspect-plugins.ps1 -Disable
    .\toggle-suspect-plugins.ps1 -Restore
#>
param(
    [switch]$Disable,
    [switch]$Restore,
    [switch]$Status
)

$base = Join-Path $env:APPDATA "XIVLauncherCN\installedPlugins"

$targets = @(
    "AutoDuty",
    "Avarice",
    "BossMod",
    "DcTraveler",
    "ICE",
    "Palantir",
    "SimpleTweaksPlugin",
    "DailyRoutines",
    "PilgrimsTraverse"
)

if (-not (Test-Path $base)) {
    Write-Error "installedPlugins not found: $base"
    exit 1
}

function Get-VersionDirs([string]$plugin) {
    $dir = Join-Path $base $plugin
    if (-not (Test-Path $dir)) { return @() }
    Get-ChildItem $dir -Directory -ErrorAction SilentlyContinue
}

function Show-Status {
    foreach ($name in $targets) {
        $dir = Join-Path $base $name
        if (-not (Test-Path $dir)) {
            "{0,-22} (not installed)" -f $name
            continue
        }

        $dirs = Get-VersionDirs $name
        $on = @($dirs | Where-Object { $_.Name -notlike "*.off" }).Count
        $off = @($dirs | Where-Object { $_.Name -like "*.off" }).Count
        "{0,-22} enabled={1} disabled={2}" -f $name, $on, $off
    }
}

function Set-Disabled([bool]$disable) {
    $failed = @()

    foreach ($name in $targets) {
        $dir = Join-Path $base $name
        if (-not (Test-Path $dir)) { continue }

        $dirs = Get-VersionDirs $name
        foreach ($verDir in $dirs) {
            $isOff = $verDir.Name -like "*.off"

            if ($disable -and -not $isOff) {
                $newName = "$($verDir.Name).off"
            }
            elseif (-not $disable -and $isOff) {
                $newName = $verDir.Name -replace '\.off$', ''
            }
            else {
                continue
            }

            try {
                Rename-Item -LiteralPath $verDir.FullName -NewName $newName -ErrorAction Stop
                "{0,-22} {1} -> {2}" -f $name, $verDir.Name, $newName
            }
            catch {
                $failed += "$name\$($verDir.Name): $($_.Exception.Message)"
            }
        }
    }

    if ($failed.Count -gt 0) {
        ""
        "Rename failed (the game is probably still running):"
        $failed | ForEach-Object { "  $_" }
    }
}

if ($Disable) { Set-Disabled $true }
elseif ($Restore) { Set-Disabled $false }
elseif ($Status) { Show-Status }
else {
    "Usage: -Status | -Disable | -Restore"
    ""
    Show-Status
}
