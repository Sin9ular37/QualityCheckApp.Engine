$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$payloadZip = Join-Path $root 'payload.zip'
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\QualityCheckApp.Engine'
$tempExtractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('QualityCheckApp.Engine.Install.' + [Guid]::NewGuid().ToString('N'))
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'QualityCheckApp.Engine.lnk'
$startMenuDir = Join-Path ([Environment]::GetFolderPath('Programs')) 'QualityCheckApp.Engine'
$startMenuShortcutPath = Join-Path $startMenuDir 'QualityCheckApp.Engine.lnk'

if (-not (Test-Path $payloadZip)) {
    throw "Installer payload not found: $payloadZip"
}

if (Test-Path $tempExtractRoot) {
    Remove-Item -LiteralPath $tempExtractRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $tempExtractRoot | Out-Null
[System.IO.Compression.ZipFile]::ExtractToDirectory($payloadZip, $tempExtractRoot)

if (Test-Path $installRoot) {
    Remove-Item -LiteralPath $installRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $installRoot | Out-Null
Copy-Item -Path (Join-Path $tempExtractRoot '*') -Destination $installRoot -Recurse -Force

if (-not (Test-Path $startMenuDir)) {
    New-Item -ItemType Directory -Path $startMenuDir | Out-Null
}

$shell = New-Object -ComObject WScript.Shell
$exePath = Join-Path $installRoot 'QualityCheckApp.Engine.exe'

$desktopShortcut = $shell.CreateShortcut($desktopShortcutPath)
$desktopShortcut.TargetPath = $exePath
$desktopShortcut.WorkingDirectory = $installRoot
$desktopShortcut.Save()

$startMenuShortcut = $shell.CreateShortcut($startMenuShortcutPath)
$startMenuShortcut.TargetPath = $exePath
$startMenuShortcut.WorkingDirectory = $installRoot
$startMenuShortcut.Save()

Remove-Item -LiteralPath $tempExtractRoot -Recurse -Force

[System.Windows.Forms.MessageBox]::Show(
    "Installation completed.`r`n`r`nApplication folder: $installRoot`r`n`r`nPlease make sure ArcGIS Engine Runtime and license are installed manually.",
    'QualityCheckApp.Engine Setup',
    [System.Windows.Forms.MessageBoxButtons]::OK,
    [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
