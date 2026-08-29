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

## Budget and completion tracking

Money is tracked in two budget pools, `SCJV` and `General`, and one credit pool mirroring the
training provider's account. Each pool reports funds added, spent, committed and free, where
*spent* is a delivered course, *committed* is a course that is scheduled but not yet delivered,
and *free* is what is left after both. A negative `SCJV` balance is the amount fronted on SCJV's
behalf and still to be invoiced back.

Three imports populate this:

| Source | How to import | Effect |
|--------|---------------|--------|
| Student register workbook | Import → migration package | Students, deliveries, allocations, top-ups; `SCJV n` tags route spending to the SCJV pool |
| Provider price list CSV | Import → CSV, type `CompletionPricing` | Dated per-course completion prices, which is what makes "completions remaining" computable |
| Provider credit history CSV | Import → CSV, type `CreditHistory` | Provider purchases and per-course debits, keyed on the provider's own transaction id so re-importing a longer export only adds new rows |

Prices are stored with an effective date rather than overwritten, so historical allocations keep
the price that applied at the time. Where the register and the provider ledger disagree, the
difference is reported on the dashboard and left for review — neither side is silently rewritten.

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

Extract the zip anywhere and run `StudentTracker.exe` — no .NET runtime install required.
Application data (database, documents, backups, logs) lives under `%LOCALAPPDATA%\StudentTracker\`.

See [docs/INSTALL.md](docs/INSTALL.md), [docs/USER_GUIDE.md](docs/USER_GUIDE.md),
[docs/BUILD.md](docs/BUILD.md) and [docs/MIGRATION.md](docs/MIGRATION.md).
