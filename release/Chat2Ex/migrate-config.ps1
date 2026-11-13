# Migrates the ChatTwo plugin configuration to Chat2Ex.
#
# Run this after the game has been closed, otherwise Dalamud and KamiToolKit overwrite the
# configuration files when the plugin is unloaded or saves its settings.

$ErrorActionPreference = 'Stop'

$cfg = Join-Path $env:APPDATA 'XIVLauncherCN\pluginConfigs'
$utf8 = New-Object System.Text.UTF8Encoding($false)

if (Get-Process -Name 'ffxiv_dx11' -ErrorAction SilentlyContinue) {
    Write-Host 'The game is still running, please close it and run this script again.' -ForegroundColor Red
    Read-Host 'Press enter to exit'
    exit 1
}

$oldConfig = Join-Path $cfg 'ChatTwo.json'
$newConfig = Join-Path $cfg 'Chat2Ex.json'
$oldAddonConfig = Join-Path $cfg 'ChatTwo\addonconfig.json'
$newAddonConfig = Join-Path $cfg 'Chat2Ex\addonconfig.json'

# 1) the plugin configuration itself, the type names inside it contain the old namespace and the
#    old assembly name
if ((Test-Path $oldConfig) -and (Test-Path $newConfig)) {
    Copy-Item $newConfig "$newConfig.bak" -Force

    $text = [System.IO.File]::ReadAllText($oldConfig, $utf8).Replace('ChatTwo', 'Chat2Ex')
    $null = $text | ConvertFrom-Json
    [System.IO.File]::WriteAllText($newConfig, $text, $utf8)

    Write-Host 'Migrated ChatTwo.json -> Chat2Ex.json' -ForegroundColor Green
}
else {
    Write-Host 'Skipped the plugin configuration, one of the files is missing.' -ForegroundColor Yellow
}

# 2) the window positions and scales that KamiToolKit stores separately
if ((Test-Path $oldAddonConfig) -and (Test-Path $newAddonConfig)) {
    Copy-Item $newAddonConfig "$newAddonConfig.bak" -Force

    $old = [System.IO.File]::ReadAllText($oldAddonConfig, $utf8) | ConvertFrom-Json
    $new = [System.IO.File]::ReadAllText($newAddonConfig, $utf8) | ConvertFrom-Json

    foreach ($property in $old.AddonConfigs.PSObject.Properties) {
        $name = $property.Name.Replace('ChatTwo_', 'Chat2Ex_')
        $new.AddonConfigs | Add-Member -NotePropertyName $name -NotePropertyValue $property.Value -Force
    }

    [System.IO.File]::WriteAllText($newAddonConfig, ($new | ConvertTo-Json -Depth 10), $utf8)

    Write-Host 'Migrated the window positions -> Chat2Ex\addonconfig.json' -ForegroundColor Green
}
else {
    Write-Host 'Skipped the window positions, one of the files is missing.' -ForegroundColor Yellow
}

Write-Host ''
Write-Host 'Done. The old files are kept in ChatTwo\ and as *.bak backups.' -ForegroundColor Green
Read-Host 'Press enter to exit'
