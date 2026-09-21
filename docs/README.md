# Student Tracker

A local, offline, single-user Windows desktop application for managing the complete administrative lifecycle of students undertaking courses.

## Features

- Student register with stable IDs and duplicate flagging
- Course definitions and scheduled course deliveries
- Student allocations and placeholder positions
- Attendance, completion, withdrawal and non-completion tracking
- Separate certificate-credit and cash-budget ledgers with personal and client-funded pool categories
- Budget prepaid-place model with manual commit, spend and reversal actions
- Position dashboard showing pool balances, completions remaining per course at current Allen cost, and reconciliation status
- Allen/provider cost vs client charge distinction across budget operations and exports
- Certificate ordering, delivery and billable-item tracking
- Course Delivery Completion Sign-Off PDF generation
- Document linking with checksums and missing-file detection
- Standard reports with CSV and PDF export
- Read-only Invoice Manager JSON/CSV cost-position snapshot exchange
- Invoicer exchange import/export
- Guided replace-all-data cutover from a canonical migration workbook
- Conventional File/Actions/Data/View/Tools/Help menu bar
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
4. Run `StudentTracker.exe` from the publish folder.

Data is stored under `%LOCALAPPDATA%\StudentTracker\` by default.

## Documentation Index

- [Current Status and Next Steps — 2026-08-29](CURRENT_STATUS_AND_NEXT_STEPS_2026-08-29.md) — current implementation status, migration readiness, verification, and prioritized next actions.
- [Technical Reference](TECHNICAL_REFERENCE.md) — architecture, data model, services, workflows, audit/logging, testing, and extension guidance.
- [Progress Report — 2026-08-29](PROGRESS_REPORT_2026-08-29.md) — implemented changes and recorded verification results.
- [Functionality Analysis](FUNCTIONALITY_ANALYSIS.md) — original function inventory and gap assessment.
- [Lifecycle Workflow Tasks — 2026-08-29](LIFECYCLE_WORKFLOW_TASKS_2026-08-29.md) — completed archive, restore, cancellation, and diagnostics backlog.
- [Build Instructions](BUILD.md).
- [Data Migration Format](DATA_MIGRATION_FORMAT.md) — exact Excel sheet/column specification for migration imports.
- [Installation](INSTALL.md).
- [User Guide](USER_GUIDE.md).
- [Migration Guide](MIGRATION.md).
- [Test Results](TEST_RESULTS.md).
