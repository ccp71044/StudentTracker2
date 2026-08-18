#Requires -Version 7.0
$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\.."
$project = Join-Path $root "src\StudentTracker.Wpf\StudentTracker.Wpf.csproj"
$publishDir = Join-Path $root "release\StudentTracker-win-x64"
$zipPath = Join-Path $root "release\StudentTracker-win-x64.zip"

Write-Host "Publishing Student Tracker (self-contained, win-x64)..."

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Published to: $publishDir"
Write-Host "Zip created:  $zipPath"
