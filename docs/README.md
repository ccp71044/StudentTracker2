# Student Tracker

A local, offline, single-user Windows desktop application for managing the complete administrative lifecycle of students undertaking courses.

## Features

- Student register with stable IDs and duplicate flagging
- Course definitions and scheduled course deliveries
- Student allocations and placeholder positions
- Attendance, completion, withdrawal and non-completion tracking
- Separate certificate-credit and cash-budget ledgers
- Certificate ordering, delivery and billable-item tracking
- Course Delivery Completion Sign-Off PDF generation
- Document linking with checksums and missing-file detection
- Standard reports with CSV and PDF export
- Invoicer exchange import/export
- Audit history
- Backup and restore

## Technology

- .NET 8 WPF desktop application
- SQLite local database via Entity Framework Core
- QuestPDF for PDF generation
- CsvHelper for CSV import/export
- Serilog for logging
- xUnit tests

## Quick Start

1. Open `StudentTracker.sln` in Visual Studio 2022 or run `dotnet build`.
2. Run `dotnet test`.
3. Publish with `installer\publish.ps1`.
4. Run `StudentTracker.Wpf.exe` from the publish folder.

Data is stored under `%LOCALAPPDATA%\StudentTracker\` by default.

See [BUILD.md](BUILD.md), [INSTALL.md](INSTALL.md), [USER_GUIDE.md](USER_GUIDE.md), and [MIGRATION.md](MIGRATION.md) for more details.
