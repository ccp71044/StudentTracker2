#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\.."
$project = Join-Path $root "src\StudentTracker.Wpf\StudentTracker.Wpf.csproj"

if (-not $Version) {
    $props = [xml](Get-Content (Join-Path $root "Directory.Build.props"))
    $Version = $props.Project.PropertyGroup.VersionPrefix
}
$Version = $Version.TrimStart("v")

$publishDir = Join-Path $root "release\StudentTracker-win-x64"
$zipPath = Join-Path $root "release\StudentTracker-win-x64-$Version.zip"

Write-Host "Publishing Student Tracker $Version (self-contained, win-x64)..."

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Published to: $publishDir"
Write-Host "Zip created:  $zipPath"
