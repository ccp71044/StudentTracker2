# Build Instructions

## Requirements

- Windows 10/11 (the WPF app and the FlaUI UI tests target `net8.0-windows`)
- .NET 8 SDK
- PowerShell 7 for `installer\publish.ps1`
- Optional: Visual Studio 2022 with .NET desktop development workload

`StudentTracker.Core`, `.Data`, `.Services` and `tests\StudentTracker.Tests` target `net8.0` and build on
any platform, so business-logic work can be done without Windows.

## Build

```powershell
dotnet restore StudentTracker.sln
dotnet build StudentTracker.sln -c Release
```

## Run Tests

```powershell
dotnet test tests\StudentTracker.Tests\StudentTracker.Tests.csproj -c Release
```

The FlaUI UI tests need a published build and an interactive Windows desktop session:

```powershell
installer\publish.ps1
dotnet test tests\StudentTracker.UITests\StudentTracker.UITests.csproj -c Release
```

## Publish Self-Contained Application

```powershell
installer\publish.ps1              # version taken from Directory.Build.props
installer\publish.ps1 -Version 1.1.0
```

The publish output is placed in `release\StudentTracker-win-x64\` and
`release\StudentTracker-win-x64-<version>.zip` is created alongside it.

## Versioning

The product version lives in `Directory.Build.props` (`VersionPrefix`) and is stamped into every assembly,
the installer zip name and the Settings screen. Keep `release\VERSION.txt` in step with it.

## Continuous Integration and Releases

- `.github/workflows/ci.yml` builds the solution, runs the unit tests and performs a publish smoke check on
  `windows-latest` for every push to `main` and every pull request.
- `.github/workflows/release.yml` runs on a `v*` tag, publishes the self-contained build and attaches
  `StudentTracker-win-x64-<version>.zip` to the GitHub release for that tag.

To cut a release: bump `Directory.Build.props` and `release\VERSION.txt`, update `release\RELEASE_NOTES.md`,
merge to `main`, then `git tag v<version> && git push origin v<version>`.
