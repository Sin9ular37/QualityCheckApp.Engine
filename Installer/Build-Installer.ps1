param(
    [string]$Configuration = 'Release',
    [string]$Platform = 'x86'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $root 'QualityCheckApp.Engine.sln'
$projectDir = Join-Path $root 'QualityCheckApp.Engine'
$buildOutput = Join-Path $projectDir (Join-Path 'bin' $Configuration)
$artifactsRoot = Join-Path $root 'artifacts\installer'
$appFilesRoot = Join-Path $artifactsRoot 'AppFiles'
$issPath = Join-Path $PSScriptRoot 'QualityCheckApp.Engine.iss'
$msbuildPath = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe'
$defaultIscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
$localIscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
$isccPath = $defaultIscc

if (-not (Test-Path $msbuildPath)) {
    throw "MSBuild not found: $msbuildPath"
}

if (-not (Test-Path $isccPath)) {
    if (Test-Path $localIscc) {
        $isccPath = $localIscc
    }
    else {
        $command = Get-Command iscc -ErrorAction SilentlyContinue
        if ($command -ne $null) {
            $isccPath = $command.Source
        }
    }
}

if (-not (Test-Path $isccPath)) {
    throw "Inno Setup compiler not found. Install Inno Setup 6 first."
}

if (-not (Test-Path $issPath)) {
    throw "Installer script not found: $issPath"
}

Write-Host '1/4 Building application...'
& $msbuildPath $solutionPath /t:Build /p:Configuration=$Configuration /p:Platform=$Platform /verbosity:minimal
if (-not $?) {
    throw 'Application build failed.'
}

if (-not (Test-Path $buildOutput)) {
    throw "Build output folder not found: $buildOutput"
}

Write-Host '2/4 Preparing installer files...'
if (Test-Path $artifactsRoot) {
    Remove-Item -LiteralPath $artifactsRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $appFilesRoot | Out-Null

$filesToCopy = Get-ChildItem -Path $buildOutput -File | Where-Object {
    $_.Name -notlike '*.pdb' -and $_.Name -notlike '*.vshost*'
}

if ($filesToCopy.Count -eq 0) {
    throw 'No publishable files were found in the build output folder.'
}

Copy-Item -Path $filesToCopy.FullName -Destination $appFilesRoot -Force

$readmePath = Join-Path $appFilesRoot 'README.txt'
$readmeContent = @'
QualityCheckApp.Engine Installer Notes

1. This installer deploys the application files, uninstall entry, and shortcuts.
2. ArcGIS Engine Runtime and license configuration must be installed manually on the target machine.
3. If the application fails to start, verify ArcGIS runtime and license first.
'@
Set-Content -Path $readmePath -Value $readmeContent -Encoding ASCII

Write-Host '3/4 Building Inno Setup package...'
& $isccPath "/DMyAppRoot=$root" "/DMyBuildOutput=$appFilesRoot" "/DMyArtifactsRoot=$artifactsRoot" $issPath
if (-not $?) {
    throw 'Inno Setup failed to build the installer.'
}

Write-Host '4/4 Installer completed.'
Write-Host "Installer created in: $artifactsRoot"
