# Student Tracker Release Notes

## Version 1.0.0

Initial build of the Student Tracker desktop application.

### Included

- Local SQLite database with automatic creation and migrations
- Student register with stable IDs
- Course definitions and course deliveries
- Student allocations and placeholder positions
- Attendance, completion, withdrawal and non-completion tracking
- Certificate-credit pools, top-ups, allocations, releases, consumption and reallocation
- Cash budget pools, commitments and expense recognition
- Certificate ordering and delivery tracking
- Course Delivery Completion Sign-Off PDF generation
- Document linking with SHA-256 checksums
- Audit history
- Backup and restore
- CSV and PDF export stubs for reports
- Invoicer exchange export stubs
- Sample seed data
- Full WPF navigation with a blue/white colour scheme, styled headers, sidebar and action buttons
- Dashboard with live summary tiles and quick-action buttons
- Add/edit/delete dialogs for Students and Courses
- Read-only student profile view with upcoming and past class history
- Student status, manager, emergency contact/phone and group/tag fields
- Budget pool setup, editing, archiving and top-up funding
- Allocation status columns with explanatory tooltips
- Dashboard, Students, Courses, Deliveries, Allocations, Certificates, Credits/Budgets, Documents, Reports, Import/Export, Settings
- Migration-package Excel importer with review queue
- Legacy `Student Tracker.xlsx` single-sheet register importer
- FlaUI-based WPF UI automation tests covering all navigation views

### Installation

Extract `StudentTracker-win-x64.zip` and run `StudentTracker.Wpf.exe`.

Data is stored under `%LOCALAPPDATA%\StudentTracker\`.

### Known Limitations

- The generic migration-package importer is configured for common column names (Students, CourseDefinitions, Deliveries, Allocations, CreditPools, BudgetPools) and may need mapping adjustments if the supplied workbook layout differs.
- The legacy `Student Tracker.xlsx` single-sheet register importer is tested against the real workbook.
- Some advanced report filters and the CSV entity importer are still stub-level and can be expanded as specific formats are finalised.
