#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Собирает self-contained single-file exe для Pmx2Vrm CLI.
.EXAMPLE
    ./build.ps1
    ./build.ps1 -Runtime win-x64 -Output dist
    ./build.ps1 -FrameworkDependent   # требует установленный .NET на целевой машине
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$Output = 'publish',
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src/Pmx2Vrm.Cli/Pmx2Vrm.Cli.csproj'
$outDir = Join-Path $root $Output
$selfContained = (-not $FrameworkDependent).ToString().ToLower()

Write-Host "Publishing Pmx2Vrm.Cli ($Configuration / $Runtime / self-contained=$selfContained)..." -ForegroundColor Cyan

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$exe = Get-ChildItem -Path $outDir -Filter 'Pmx2Vrm.Cli*' |
    Where-Object { $_.Extension -in '.exe', '' -and -not $_.PSIsContainer } |
    Select-Object -First 1

Write-Host "`nBuilt: $($exe.FullName)" -ForegroundColor Green
