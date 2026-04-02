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
$stageRoot = Join-Path $artifactsRoot 'stage'
$payloadRoot = Join-Path $stageRoot 'Payload'
$payloadZip = Join-Path $stageRoot 'payload.zip'
$sedPath = Join-Path $stageRoot 'QualityCheckApp.Engine.sed'
$targetExe = Join-Path $artifactsRoot 'QualityCheckApp.Engine-Setup.exe'
$msbuildPath = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe'
$iexpressPath = 'C:\Windows\System32\iexpress.exe'

if (-not (Test-Path $msbuildPath)) {
    throw "MSBuild not found: $msbuildPath"
}

if (-not (Test-Path $iexpressPath)) {
    throw "IExpress not found: $iexpressPath"
}

Write-Host '1/5 Building application...'
& $msbuildPath $solutionPath /t:Build /p:Configuration=$Configuration /p:Platform=$Platform /verbosity:minimal
if (-not $?) {
    throw 'Application build failed.'
}

if (-not (Test-Path $buildOutput)) {
    throw "Build output folder not found: $buildOutput"
}

Write-Host '2/5 Preparing installer staging...'
if (Test-Path $artifactsRoot) {
    Remove-Item -LiteralPath $artifactsRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $payloadRoot | Out-Null

$filesToCopy = Get-ChildItem -Path $buildOutput -File | Where-Object {
    $_.Name -notlike '*.pdb' -and $_.Name -notlike '*.vshost*'
}

if ($filesToCopy.Count -eq 0) {
    throw 'No files were found in the build output folder.'
}

Copy-Item -Path $filesToCopy.FullName -Destination $payloadRoot -Force
Copy-Item -Path (Join-Path $PSScriptRoot 'InstallApp.ps1') -Destination $stageRoot -Force
Copy-Item -Path (Join-Path $PSScriptRoot 'InstallApp.cmd') -Destination $stageRoot -Force

$readmePath = Join-Path $payloadRoot 'README.txt'
$readmeContent = @'
QualityCheckApp.Engine Installer Notes

1. This installer only deploys the application files and shortcuts.
2. ArcGIS Engine Runtime and license configuration must be installed manually on the target machine.
3. If the app fails to start, verify the ArcGIS runtime and license environment first.
'@
Set-Content -Path $readmePath -Value $readmeContent -Encoding ASCII

Write-Host '3/5 Creating payload zip...'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($payloadRoot, $payloadZip)

Write-Host '4/5 Generating IExpress configuration...'
$targetExeEscaped = $targetExe.Replace('\', '\\')
$stageRootEscaped = ($stageRoot + '\').Replace('\', '\\')
$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=Installation completed. Make sure ArcGIS Engine Runtime and license are installed manually.
TargetName=$targetExeEscaped
FriendlyName=QualityCheckApp.Engine Setup
AppLaunched=InstallApp.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=InstallApp.cmd
UserQuietInstCmd=InstallApp.cmd
SourceFiles=SourceFiles

[SourceFiles]
SourceFiles0=$stageRootEscaped

[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=

[Strings]
FILE0=InstallApp.cmd
FILE1=InstallApp.ps1
FILE2=payload.zip
"@
Set-Content -Path $sedPath -Value $sed -Encoding ASCII

Write-Host '5/5 Building installer exe...'
& $iexpressPath /N $sedPath
if (-not $?) {
    throw 'IExpress failed to build the installer.'
}

Write-Host "Installer created: $targetExe"
