# Build Instructions

## Requirements

- Windows 10/11
- .NET 8 SDK
- Optional: Visual Studio 2022 with .NET desktop development workload

## Build

```powershell
dotnet restore
dotnet build
```

## Run Tests

```powershell
dotnet test
```

## Publish Self-Contained Application

```powershell
installer\publish.ps1
```

The publish output is placed in `release\StudentTracker-win-x64\` and a zip is created in `release\`.
