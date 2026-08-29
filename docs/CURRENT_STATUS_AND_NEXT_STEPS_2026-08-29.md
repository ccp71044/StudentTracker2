# Student Tracker 2 — Current Status and Next Steps

**Status date:** 29 August 2026  
**Branch:** `main`  
**Baseline commit at report creation:** `3ddf060` — `Expose complete operational workflows and reporting.`

## Executive status

Student Tracker 2 is now a buildable, self-contained .NET 8 WPF application with the principal student, course, delivery, allocation, certificate, finance, document, migration, reporting, audit, backup, and export workflows exposed through the user interface.

The major functionality and lifecycle gaps identified in the earlier reviews have been addressed. Safe inline table editing is available for ordinary metadata while lifecycle and financial state changes remain command-driven. The current priority is no longer broad feature construction; it is controlled production-data loading, user acceptance testing, deeper workflow automation, and correction of any issues found with real operating data.

## Completed application areas

### Students

- Create, view, edit, search, archive, and restore students.
- Stable student display identifiers and duplicate indicators.
- View a student's allocation/course history.
- Create an allocation directly for a selected student.
- Table selection, double-click behavior, refresh, and right-click actions.

### Courses and deliveries

- Create, edit, search, archive, and restore course definitions.
- Store and edit course duration in days.
- Create a delivery directly for a selected course.
- Create, edit, cancel, and refresh deliveries.
- Display course, status, capacity, dates, location, and trainer details.
- View or add allocations for a selected delivery.
- Table selection, double-click behavior, and right-click actions.

### Allocations

- Allocate a student to a delivery.
- Create and replace placeholder allocations.
- Edit, transfer, cancel, and refresh allocations.
- Record attendance and outcomes.
- Connect allocations to budget and certificate-credit pools.
- Apply cancellation safeguards to financial and credit state.
- Table selection, double-click behavior, and right-click actions.

The design supports one student undertaking multiple courses because each student-course-delivery relationship is represented by a separate allocation.

### Certificates

- Create certificate orders from eligible allocations.
- Record delivery against a selected order.
- View order details and delivery history.
- Track provider, status, quantity, replacement state, delivery method, recipient, evidence, and billable state.
- Table selection, double-click behavior, and right-click actions.

### Credits and budgets

- Create, edit, archive, and restore budget and credit pools.
- Add budget funds and certificate credits.
- View budget and credit transaction histories.
- Display actual and forecast budget availability.
- Use financial and credit lifecycle safeguards from allocation and certificate workflows.
- Double-click and right-click actions on pool tables.

### Documents

- Add, open, archive, and restore documents.
- Edit document metadata.
- Link documents to students, allocations, deliveries, and certificate orders using friendly identifiers.
- Prevent duplicate links.
- Check for missing physical files.
- Table selection, double-click behavior, and right-click actions.

### Reports and exports

The Reports screen now includes the original completion/certificate reports plus operational and control reports covering:

- Completed, withdrawn, and non-completing students.
- Certificates awaiting order or delivery and certificates delivered.
- Upcoming, cancelled, and completed deliveries.
- Delivery capacity.
- Active, transferred, cancelled, and placeholder allocations.
- Attendance.
- Course utilisation and completion rates.
- Budget summaries and transaction history.
- Credit summaries and transaction history.
- Certificate orders, replacements, and turnaround.
- Audit activity.
- Import-review items.

CSV export commands are available for the expanded report set. Course, student, allocation, and invoicer exports are also available from Import / Export.

### Inline table editing

An explicit **Edit Table** toggle is available for students, courses, deliveries, documents, budget pools, and credit pools. Tables remain read-only until it is enabled.

Editable fields are limited to ordinary metadata such as names, contact details, providers, dates, capacities, descriptions, and notes. Display identifiers, archive/active state, delivery status, allocation status, attendance, outcomes, certificate lifecycle state, balances, and transactions remain read-only in tables and must be changed through their validated workflow commands. Committed inline edits use the existing update services and therefore create audit records; failed updates show an error and reload the stored values.

### Lifecycle, audit, and diagnostics

- Archive/deactivate operations preserve historical records.
- Restore workflows exist for recoverable entities.
- Confirmation prompts protect archive, restore, cancellation, and similar actions.
- Dependency checks block unsafe lifecycle changes.
- Successful and blocked actions are recorded in the database audit log.
- Serilog writes daily rolling diagnostic files under `%LOCALAPPDATA%\StudentTracker\Logs\` with 30-day retention.

### Migration and release

- The canonical multi-sheet migration workbook format is documented.
- The importer preserves supplied student and delivery identifiers.
- Student, course, delivery, allocation, budget-pool, and certificate-credit-pool relationships are imported.
- Dates, booleans, statuses, lifecycle flags, pool references, and course duration are supported.
- Legacy worksheet aliases remain supported where practical.
- The user-facing executable is named `StudentTracker.exe`.

## Current migration dataset

The validated authoritative workbook is:

`C:\Users\AlexGillam\OneDrive - townandcountrymedical.com.au\Student_Tracker_2_Comprehensive_Migration_Dataset.xlsx`

Validation previously confirmed:

- 32 students.
- 23 course definitions.
- 37 course deliveries.
- 53 allocations.
- 1 certificate credit pool.
- No duplicate student, course, or delivery identifiers.
- No broken student, delivery, or course relationships.
- Valid allocation, attendance, and outcome values.
- No merged cells or formulas.

**Important:** this workbook has been validated but has not yet been loaded into the live application database.

## Recorded verification

| Check | Result |
|---|---:|
| Release solution build | Passed |
| Unit tests | 45 passed, 0 failed |
| FlaUI tests | 11 passed, 0 failed |
| Self-contained Windows publish | Passed |
| Git synchronization before this report | Clean and synchronized |

The 11 FlaUI tests are navigation and smoke tests. They do not yet automate every newly added dialog, context menu, confirmation, import, export, or end-to-end financial workflow.

## Published application

The current self-contained executable is:

`C:\Users\AlexGillam\OneDrive\Programming - Gillams Software\Student Tracker 2\release\StudentTracker-win-x64\StudentTracker.exe`

Application data is stored by default under:

`%LOCALAPPDATA%\StudentTracker\`

## Work needed next

### 1. Load and reconcile the authoritative data

This is the immediate operational next step.

1. Back up the current application database.
2. Inspect the current database for earlier incorrect or test imports.
3. Decide whether to retain, archive, or replace those records before importing; do not append the authoritative workbook blindly if duplicate data already exists.
4. Import the comprehensive migration workbook through **Import / Export → Import Migration Package**.
5. Review all importer warnings.
6. Reconcile counts and sample records across Students, Courses, Deliveries, Allocations, Credits/Budgets, and Reports.
7. Create a post-import backup after acceptance.

### 2. Perform structured user acceptance testing

Use representative real workflows to confirm:

- A student can undertake multiple courses without overwriting prior history.
- Placeholder replacement and allocation transfer preserve audit and financial state.
- Attendance and outcome updates appear in reports.
- Certificate ordering and delivery update lifecycle and billable state correctly.
- Budget and credit balances match source records.
- Document links open the intended files and records.
- Archive, restore, and cancellation safeguards behave as expected.
- CSV outputs match operational requirements.

### 3. Expand FlaUI workflow coverage

Add end-to-end tests for:

- Add/edit/archive/restore for each principal record type.
- Right-click and double-click table behavior.
- Placeholder creation/replacement and transfers.
- Attendance, outcomes, and cancellation.
- Certificate order and delivery.
- Budget funding, credit top-up, and transaction history.
- Document metadata and linking.
- Migration import and report export.
- Confirmation and error dialogs.

### 4. Strengthen duplicate-allocation protection

The model correctly permits one student to have many courses. It currently also permits two active allocations for the same student and exact delivery. Add service validation—and, after checking legacy data, an appropriate database constraint or active-record rule—to prevent accidental duplicate enrolment while preserving legitimate transfer/history scenarios.

### 5. Improve asynchronous loading diagnostics

Several view models start asynchronous loading from constructors. Move initial loading to a consistent awaited view lifecycle and surface/log load failures so startup data errors cannot become unobserved task failures.

### 6. Complete report interaction and output polish

Potential refinements include:

- Navigate from a report row to its student, allocation, delivery, order, or pool.
- Add user-selectable report columns and saved filters if needed.
- Confirm which reports require PDF output in addition to CSV.
- Verify certificate delivered dates against certificate-delivery records in every output.
- Add print-friendly headings, generation timestamps, and filter summaries.

### 7. Resolve existing test analyzer warnings

Some older unit tests use blocking task operations and produce `xUnit1031` analyzer warnings during a full rebuild. Convert those tests to `async Task` and `await` to remove the warnings and reduce deadlock risk.

## Release readiness assessment

The application is ready for controlled data migration and user acceptance testing. It should not yet be treated as fully production-accepted until the authoritative workbook has been imported into a backed-up database, the imported totals have been reconciled, and the key workflows have been exercised with real records.

No broad service/UI functionality gap is currently known to block that controlled validation phase. The remaining work is primarily data cutover, deeper automated workflow coverage, integrity hardening, and user-driven refinement.
