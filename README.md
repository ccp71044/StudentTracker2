# Student Tracker

A local, offline, single-user Windows desktop application for managing the complete
administrative lifecycle of students undertaking courses.

.NET 8 WPF, SQLite via Entity Framework Core, QuestPDF, CsvHelper, Serilog, xUnit.

## Repository layout

| Path | Contents |
|------|----------|
| `src/StudentTracker.Core` | Domain models, enums, shared primitives |
| `src/StudentTracker.Data` | `DbContext` and EF Core migrations |
| `src/StudentTracker.Services` | Business logic (students, credits, budgets, certificates, imports, reports) |
| `src/StudentTracker.Wpf` | WPF application (views, view models, DI composition root) |
| `tests/StudentTracker.Tests` | xUnit unit/integration tests (cross-platform) |
| `tests/StudentTracker.UITests` | FlaUI WPF UI automation tests (Windows only) |
| `installer/publish.ps1` | Self-contained win-x64 publish + zip |
| `docs/` | Build, install, user and migration documentation |

## Build and test

```powershell
dotnet restore StudentTracker.sln
dotnet build StudentTracker.sln -c Release
dotnet test tests\StudentTracker.Tests\StudentTracker.Tests.csproj -c Release
```

The WPF application and the FlaUI UI tests target `net8.0-windows` and require Windows.
`Core`, `Data`, `Services` and the unit tests target `net8.0` and build on any platform.

## Releasing

Releases are produced by CI, not by hand.

1. Set the new version in `Directory.Build.props` (`VersionPrefix`) and `release/VERSION.txt`,
   add an entry to `release/RELEASE_NOTES.md`, and merge to `main`.
2. Tag the merge commit and push the tag:

   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

3. The `release` workflow builds a self-contained win-x64 single-file build, zips it as
   `StudentTracker-win-x64-<version>.zip` and publishes it on the GitHub release for the tag.

To produce the same artifact locally on Windows, run `installer\publish.ps1`.

## Installing

Extract the zip anywhere and run `StudentTracker.Wpf.exe` — no .NET runtime install required.
Application data (database, documents, backups, logs) lives under `%LOCALAPPDATA%\StudentTracker\`.

See [docs/INSTALL.md](docs/INSTALL.md), [docs/USER_GUIDE.md](docs/USER_GUIDE.md),
[docs/BUILD.md](docs/BUILD.md) and [docs/MIGRATION.md](docs/MIGRATION.md).
