# Student Tracker 2 Progress Report — 2026-08-29

## Executive summary

Student Tracker 2 has progressed from a partially exposed service-layer application to an integrated WPF desktop system in which the main student, course, delivery, allocation, certificate, finance, document, reporting, backup, import, and export workflows are accessible through the UI.

The implementation work was driven by the gap assessment in `FUNCTIONALITY_ANALYSIS.md` and the lifecycle safety backlog in `LIFECYCLE_WORKFLOW_TASKS_2026-08-29.md`. The current `main` branch has passed the solution Release build, 25 unit tests, and 11 FlaUI tests.

## Repository synchronization and baseline

- Repository: `https://github.com/ccp71044/StudentTracker2.git`
- Branch: `main`
- Initial synchronized baseline: `7b9f473`
- First integration commit: `667b796` — `Update Student Tracker 2`
- Feature-completion commit: `7ff1a28` — `Add certificate order, credit pool, and export UI features.`
- Lifecycle-safety commit: `3572534` — `Make lifecycle actions safe and recoverable`
- Current local and remote branches were synchronized after implementation.

The original FlaUI baseline was 11 passing tests. That result remained at 11 passing tests after the changes.

## Completed functionality analysis

A detailed inventory and gap analysis was created in `FUNCTIONALITY_ANALYSIS.md`. It covers:

- Service-layer capabilities.
- UI navigation and available controls.
- Missing create, update, selection, and lifecycle actions.
- Table interaction gaps.
- Import/export coverage.
- Reporting coverage.
- Recommended implementation priorities.

## Allocation management

Implemented an allocation-management dialog and connected it to the Allocations view.

The UI now supports:

- Creating an allocation.
- Selecting a student and course delivery.
- Editing allocation, attendance, outcome, certificate, budget, and credit-related fields supported by the domain model.
- Opening an allocation for editing by double-clicking its table row.
- Explicitly cancelling a selected allocation.

Cancellation safeguards now:

- Require confirmation.
- Reject cancellation after a certificate has been ordered or credit consumed.
- Release recorded pending budget commitments.
- Release recorded allocated credit.
- Mark the allocation and outcome as cancelled.
- Set an outcome date and retain the cancellation reason.
- Record successful and blocked actions in the audit trail.

## Course-delivery management

Implemented delivery creation and editing through a modal dialog.

The delivery workflow now supports:

- Course selection.
- Start and end dates.
- Date status.
- Trainer and location details.
- Capacity, delivery status, and notes.
- Double-click row editing/viewing.
- Explicit delivery cancellation.

Delivery cancellation is blocked while non-terminal allocations remain. Users must first cancel, withdraw, transfer, or finalise those allocations. Successful and blocked cancellation attempts are audited.

## Certificate lifecycle

Implemented certificate order and delivery dialogs.

Certificate ordering supports:

- Selecting an eligible completed allocation.
- Provider and external-reference capture.
- Notes.
- Replacement orders and replacement reasons.
- Controlled eligibility override.
- Existing service validation for completed outcomes, allocated credit, duplicate normal orders, and linked credit pools.

Certificate delivery recording supports:

- Selecting an outstanding certificate order.
- Delivery date.
- Delivery method and recipient.
- Optional evidence document.
- Notes.

These operations remain governed by `CertificateService`, including credit consumption, allocation certificate statuses, billable triggers, and audit entries.

## Student and course lifecycle

Student and course tables now support double-click editing and consistent archive terminology.

Students:

- Archive is a soft operation using `IsArchived` and `IsActive`.
- Archiving is blocked while active allocations exist.
- A Show archived option exposes archived records.
- Restore reverses the archive state.
- Archive, restore, and blocked attempts are audited.

Course definitions:

- The former Delete label is now Archive.
- Archiving sets `IsActive` to false.
- Archiving is blocked while active deliveries exist.
- A Show inactive option exposes archived course definitions.
- Restore reactivates a course.
- Historical deliveries and allocations remain linked.

## Budget and certificate-credit pools

Budget pool management already supported creation, editing, funding, and archive operations. Certificate-credit pool CRUD was added, and both pool types now use safer lifecycle controls.

Budget pools:

- Add, edit, add funds, archive, restore, and show inactive.
- Archive is blocked while pending cash commitments exist.
- Transactions remain available for historical calculations.

Certificate-credit pools:

- Add, edit, archive, restore, and show inactive.
- Archive is blocked while allocated credits remain.
- Transactions remain preserved.
- Available-credit calculations now account for release and reallocation-out transactions.

All successful and blocked lifecycle changes are audited.

## Document management

Document management now supports:

- Adding managed documents.
- Listing all managed documents correctly.
- Opening a selected document through the registered Windows application.
- Detecting missing physical files.
- Archiving instead of physically deleting a document.
- Showing archived documents.
- Restoring archived documents.
- Double-click opening.

Document archive retains the managed file and metadata. It is blocked when the document is referenced as certificate-delivery evidence. Restoring a document sets it to Active if its file exists or Missing if the file no longer exists.

## Reporting

The Reports view now exposes:

- Completed students.
- Students awaiting certificate order.
- Withdrawn students, separated according to cost handling.
- Non-completions.
- Certificates awaiting delivery.
- Certificates delivered.

Added report controls:

- From date.
- To date.
- Include archived students.
- Apply/Refresh.
- CSV export for completed, withdrawn, non-completion, awaiting-delivery, and delivered reports.

Date filters use inclusive calendar-day semantics for the selected end date. The certificate-awaiting-order query was also grouped explicitly to avoid status/operator-precedence ambiguity.

## Import, export, backup, and migration

The Import / Export view supports:

- Creating a backup.
- Restoring a backup.
- Exporting an Invoicer batch.
- Exporting active course definitions to CSV.
- Exporting active students to CSV.
- Exporting all allocations to CSV.
- Importing a migration workbook.
- Importing completion-pricing CSV data.
- Importing provider credit-history CSV data.

Export labels now explicitly state whether active or all records are included.

## Table interaction

Double-click actions were added to the main operational tables:

- Students: edit.
- Courses: edit.
- Deliveries: edit/view.
- Allocations: edit.
- Documents: open.

Commands continue to depend on `SelectedItem`, with command enablement refreshed when selection changes.

## Confirmation and error handling

`IDialogService` now provides:

- Modal view-model dialogs.
- Yes/No confirmation prompts.
- Error display with exception logging.

Archive, restore, and cancellation commands use confirmation prompts. Expected business-rule failures are shown to the user and written to the application log instead of being silently swallowed.

## Audit and `.log` files

Business-level audit entries are stored in the SQLite `AuditLogs` table. New lifecycle operations audit:

- Archived.
- Restored.
- Cancelled.
- ArchiveBlocked.
- CancellationBlocked.

Blocked records include contextual counts or reasons where relevant.

Serilog writes diagnostic logs to:

`%LOCALAPPDATA%\StudentTracker\Logs\student-tracker-YYYYMMDD.log`

The file logger uses:

- Information minimum level.
- Daily rolling files.
- 30 retained files.
- Shared-file access.
- Two-second flush-to-disk interval.
- Timestamp, severity, message, and exception output.
- Explicit flush during application exit.
- Fatal startup and AppDomain exception capture.
- Dispatcher unhandled-exception capture.
- Dialog-service logging for handled workflow errors.

## Verification results

Final recorded verification:

| Check | Result |
|---|---:|
| Release solution build | Passed — 0 warnings, 0 errors |
| Unit tests | Passed — 25/25 |
| FlaUI tests | Passed — 11/11 |
| Git synchronization | `main` synchronized with `origin/main` |

The first parallel Release build attempt encountered locked WPF output DLLs because a previous Student Tracker process remained running. The stale process was stopped and the build was rerun successfully with no warnings or errors.

## Current status

The high- and medium-priority gaps identified in the original implementation backlog are complete. Lifecycle actions are now substantially safer, reversible where appropriate, and auditable. The application builds and its automated test suites pass.

Further enhancements can focus on deeper validation previews for imports, richer report composition, more comprehensive lifecycle test coverage, role-based access if the single-user scope changes, and additional FlaUI coverage for the newly added dialogs and confirmation flows.
