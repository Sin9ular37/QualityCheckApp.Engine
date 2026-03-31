param()

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$nugetDir = Join-Path $root '.tools'
$nugetPath = Join-Path $nugetDir 'nuget.exe'
$source = 'https://www.nuget.org/api/v2/'

if (-not (Test-Path $nugetDir)) {
    New-Item -ItemType Directory -Path $nugetDir | Out-Null
}

if (-not (Test-Path $nugetPath)) {
    Invoke-WebRequest -UseBasicParsing 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile $nugetPath
}

& $nugetPath install (Join-Path $root 'QualityCheckApp.Engine\packages.config') -OutputDirectory (Join-Path $root 'packages') -Source $source -NonInteractive
