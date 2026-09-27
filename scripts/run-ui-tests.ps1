#Requires -Version 7.0
<#
.SYNOPSIS
    Publishes Student Tracker and runs the offline FlaUI UI suite against it.

.DESCRIPTION
    Must be run on Windows 10/11 in an interactive desktop session: UI Automation drives the real
    desktop, so a locked screen, an RDP session that has been minimised, or a service account will
    fail. Nothing here touches the network or the operator's own %LOCALAPPDATA%\StudentTracker -
    each test class launches the app against its own temporary data root.

.EXAMPLE
    pwsh -File scripts\run-ui-tests.ps1

.EXAMPLE
    pwsh -File scripts\run-ui-tests.ps1 -Filter "FullyQualifiedName~ButtonTests"
#>
[CmdletBinding()]
param(
    # Skip the publish step and test whatever is already in release\StudentTracker-win-x64.
    [switch]$SkipPublish,
    # xUnit filter expression, e.g. "FullyQualifiedName~SmokeTests".
    [string]$Filter
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\.."
$uiTests = Join-Path $root "tests\StudentTracker.UITests\StudentTracker.UITests.csproj"
$results = Join-Path $root "release\ui-test-results"

if (-not $IsWindows) { throw "The UI suite drives a WPF window and only runs on Windows." }

if (-not $SkipPublish) {
    & (Join-Path $root "installer\publish.ps1")
    if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE" }
}

$exe = Join-Path $root "release\StudentTracker-win-x64\StudentTracker.Wpf.exe"
if (-not (Test-Path $exe)) { throw "No published executable at $exe. Run without -SkipPublish." }

# Point the fixture at the published build rather than letting it search for one.
$env:STUDENTTRACKER_EXE = $exe

if (Test-Path $results) { Remove-Item $results -Recurse -Force }
New-Item -ItemType Directory -Path $results | Out-Null

Write-Host "Running the UI suite against $exe"
Write-Host "Leave the desktop alone while it runs - the tests use real keyboard and mouse input."

$arguments = @(
    "test", $uiTests,
    "-c", "Release",
    "--logger", "trx;LogFileName=ui-tests.trx",
    "--results-directory", $results
)
if ($Filter) { $arguments += @("--filter", $Filter) }

& dotnet @arguments
$exitCode = $LASTEXITCODE

$artifacts = Get-ChildItem -Path (Join-Path $root "tests\StudentTracker.UITests\bin\Release") `
    -Recurse -Directory -Filter "ui-test-artifacts" -ErrorAction SilentlyContinue
foreach ($artifact in $artifacts) {
    Copy-Item -Path (Join-Path $artifact.FullName "*") -Destination $results -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Results and failure screenshots: $results"
exit $exitCode
