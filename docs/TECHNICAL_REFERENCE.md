# Student Tracker 2 Technical Reference

Last updated: 2026-08-29

## 1. Purpose and operating model

Student Tracker 2 is a local, offline, single-user Windows desktop application for managing students through course enrolment, attendance, outcomes, certificate ordering and delivery, budget and credit accounting, document retention, reporting, and integration exports.

The application uses a local SQLite database and managed filesystem directories under `%LOCALAPPDATA%\StudentTracker\` by default. It does not depend on a continuously available server.

## 2. Solution structure

| Project | Target | Responsibility |
|---|---|---|
| `src/StudentTracker.Core` | `net8.0` | Domain models, enums, and shared entity abstractions |
| `src/StudentTracker.Data` | `net8.0` | EF Core DbContext, SQLite configuration, migrations, and database bootstrap |
| `src/StudentTracker.Services` | `net8.0` | Business workflows, ledgers, imports, exports, reports, backup, documents, and audit operations |
| `src/StudentTracker.Wpf` | `net8.0-windows` | WPF UI, MVVM view models, navigation, dialogs, startup, DI, and file logging |
| `tests/StudentTracker.Tests` | `net8.0` | xUnit service and business-logic tests |
| `tests/StudentTracker.UITests` | `net8.0-windows` | FlaUI UI automation tests |

The main solution file is `StudentTracker.sln`.

## 3. Technology stack

- .NET 8.
- WPF.
- CommunityToolkit.Mvvm source generators and commands.
- Microsoft.Extensions.DependencyInjection.
- Entity Framework Core with SQLite.
- CsvHelper.
- ClosedXML for migration workbooks.
- QuestPDF for PDF generation.
- Serilog with the file sink.
- xUnit.
- FlaUI UIA3.

## 4. Startup and dependency injection

`App.xaml.cs` owns application startup.

Startup sequence:

1. Register dispatcher and AppDomain unhandled-exception handlers.
2. Construct the service collection.
3. Register application settings, data locations, dialog services, DbContext, domain services, and view models.
4. Create required data directories.
5. Configure Serilog.
6. Create and migrate the SQLite database.
7. Ensure application settings and outcome-reason seed data exist.
8. Optionally seed demonstration data when `--sample-data` is supplied.
9. Resolve `MainViewModel` and show `MainWindow`.

Service lifetimes are currently:

- Singleton: `AppSettings`, `DataLocationService`, `IDialogService`, `DatabaseBootstrap`.
- Scoped: DbContext, business services, navigation view models, and edit-dialog view models.

The desktop application builds one root provider. The current design therefore treats scoped dependencies as application-lifetime objects unless a nested scope is explicitly introduced.

## 5. Data locations

`DataLocationService` derives all operational paths from `AppSettings.DataRootPath`.

Default root:

`%LOCALAPPDATA%\StudentTracker\`

Directory layout:

```text
StudentTracker\
  Database\student-tracker.db
  Documents\
    Students\
    Courses\
    CourseDeliveries\
    SignOffs\
    Certificates\
    Invoices\
    Reports\
    General\
  Imports\
  Exports\
  Integration\
    InvoicerImport\
    InvoicerExport\
    Processed\
    Errors\
  Backups\
  Logs\
  Templates\
```

`EnsureDirectories()` creates this structure at startup. SQLite foreign-key enforcement is enabled in the connection string.

## 6. Persistence model

`StudentTrackerDbContext` exposes these principal sets:

- Students.
- Course definitions, prices, and deliveries.
- Allocations and outcome reasons.
- Certificate-credit pools and transactions.
- Budget pools, transactions, and funding sources.
- Certificate orders and deliveries.
- Invoices.
- Sign-offs and participants.
- Documents and document links.
- Audit logs.
- Application settings.
- Export batches and items.
- Import review queue records.

Operational enums are persisted as strings, improving database readability and reducing dependence on enum numeric order.

Important indexes include:

- Allocation student/delivery pair.
- Document-link entity type/entity ID.
- Audit entity type.
- Course match key.
- Course-price definition/effective date.
- External certificate-credit transaction ID.

## 7. Domain identifiers

`DisplayIdGenerator` assigns stable human-readable identifiers to records where required. Prefixes include:

- `STU` — student.
- `DEL` — course delivery.
- `ALL` — allocation.
- `CRP` — certificate-credit pool.
- `CTX` — credit transaction.
- `BUD` — budget pool.
- `BTX` — budget transaction.
- `ORD` — certificate order.
- `CDV` — certificate delivery.
- `DOC` — document.

Database GUIDs remain the internal relational keys.

## 8. MVVM and navigation

The WPF application uses CommunityToolkit.Mvvm:

- `[ObservableProperty]` generates bindable properties.
- `[RelayCommand]` generates `ICommand` implementations.
- Partial property-change methods update command availability or reload filtered data.

`MainViewModel` owns one view model for each navigation area and changes `CurrentViewModel` when a sidebar command is invoked.

Navigation areas:

1. Dashboard.
2. Students.
3. Courses.
4. Deliveries.
5. Allocations.
6. Certificates.
7. Credits & Budgets.
8. Documents.
9. Reports.
10. Import / Export.
11. Settings.

`MainWindow.xaml` maps view-model types to views with WPF data templates. Sidebar buttons and view headers carry automation IDs used by FlaUI tests.

## 9. Dialog infrastructure

`IDialogService` provides:

```csharp
bool? ShowDialog<TViewModel>(TViewModel viewModel);
bool Confirm(string message, string title = "Confirm action");
void ShowError(string message, Exception? exception = null, string title = "Student Tracker");
```

`DialogService` discovers matching `*ViewModel` and `*View` types by naming convention. Dialog views must have a public parameterless constructor. The service assigns the supplied view model as the view and window DataContext.

View models implementing `ICloseable` raise `RequestClose`, allowing Save and Cancel commands to set the dialog result.

Confirmation prompts use a warning-styled Yes/No `MessageBox`. Error prompts write exception details through Serilog and display a user-safe message.

## 10. Student workflow

`StudentService` supports lookup, active/archived searching, create, update, duplicate detection, archive, and restore.

Archive behavior:

- Requires UI confirmation.
- Is blocked while the student has active allocations.
- Sets `IsArchived = true` and `IsActive = false`.
- Retains allocations, documents, audit history, and relational identity.
- Removes the student from default search results.

Restore reverses those flags. The Students view includes Show archived and Restore controls.

Duplicate detection compares active students by case-insensitive full name or email and sets `PotentialDuplicate` rather than rejecting entry.

## 11. Course and delivery workflow

### Course definitions

`CourseService` manages course definitions and scheduled deliveries.

Course definition archive:

- Is a soft deactivate using `IsActive`.
- Is blocked while a non-completed/non-cancelled delivery exists.
- Retains historical prices, deliveries, and allocations.
- Can be reversed through Restore.

### Course deliveries

A delivery references a course definition and can hold dates, date confidence, location, trainer details, capacity, operational status, and notes.

Delivery cancellation:

- Requires confirmation.
- Is blocked while active allocations exist.
- Sets `DeliveryStatus` to `Cancelled`.
- Preserves the delivery and its historical relationships.

Terminal allocation states considered safe for delivery cancellation are Cancelled, Finalised, Withdrawn, and Transferred.

## 12. Allocation workflow

An allocation connects a student or placeholder to a course delivery and carries operational, attendance, outcome, certificate, credit, cash, billing, and export state.

Key state families:

- Allocation status: Reserved, Enrolled, Active, Transferred, Withdrawn, Finalised, Cancelled.
- Attendance status: NotRecorded, Confirmed, Attended, PartiallyAttended, DidNotAttend, Exempt.
- Outcome status: Pending, Completed, NotCompleted, Withdrawn, Transferred, Cancelled, HistoricalReviewRequired.
- Credit status: None, Allocated, Consumed, Released, Reallocated, Expired, Unavailable, ReviewRequired.
- Cash commitment status: None, Pending, Released, Spent, ReviewRequired.

`AllocationService` supports:

- Standard allocation.
- Placeholder creation.
- Replacing a placeholder with a student.
- Attendance updates.
- Outcome updates.
- Transfer.
- Cancellation.

Cancellation rules:

- A certificate order or consumed credit blocks cancellation.
- Pending budget commitments are released using the recorded commitment balance.
- Allocated credit is released using the recorded allocation/reservation balance.
- Allocation and outcome statuses become Cancelled.
- Outcome date and reason are retained.
- Financial release transactions and the cancellation audit entry are committed.

The allocation grid supports selected-row editing and explicit cancellation.

## 13. Certificate workflow

`CertificateService` controls ordering and delivery.

Normal certificate-order eligibility requires:

- A completed allocation.
- Allocated certificate credit.
- A linked certificate-credit pool.
- No existing outstanding normal order for the allocation.

The order operation:

1. Resolves certificate cost from allocation or course defaults.
2. Creates a certificate order.
3. Consumes credit.
4. Sets allocation order status to Ordered.
5. Sets delivery status to Awaiting.
6. Links the credit transaction.
7. Applies the configured billable trigger.
8. Audits the operation.

Replacement orders can be identified with a replacement reason. An explicit eligibility override is available for controlled exceptional cases.

Delivery recording creates a certificate-delivery record, updates allocation delivery status, optionally links an evidence document, recalculates billing, and audits delivery.

## 14. Certificate-credit ledger

`CreditService` manages credit pools and their append-style transaction ledger.

Pool operations:

- Create.
- Update.
- Archive.
- Restore.
- Top up.
- Query loaded, allocated, consumed, expired, and available balances.

Allocation operations:

- Allocate.
- Consume.
- Release.
- Reallocate between allocations/pools.

Archived pools remain in the database and can be shown or restored. Archive is blocked while allocations remain in Allocated credit status.

Available credit is derived from transaction categories. Release and reallocation-out values reduce the current allocated balance rather than deleting historical allocation transactions.

## 15. Cash-budget ledger

`BudgetService` manages budget pools and transactions.

Pool operations:

- Create.
- Update.
- Add funds.
- Archive.
- Restore.

Allocation financial operations:

- Create commitment.
- Release commitment.
- Recognise expense.

Calculated values:

- Funds added.
- Actual expenditure.
- Pending commitments.
- Actual available funds.
- Forecast available funds.

Archive is blocked while pending commitments remain. Existing transactions are retained and continue to support historical reporting.

## 16. Documents

`DocumentService` copies source files into the managed Documents directory. It records:

- Original and stored file names.
- Relative managed path.
- Extension and MIME type.
- Size.
- SHA-256 checksum.
- Display name and description.
- Received date.
- Active, Missing, or Archived status.

Documents can be linked to arbitrary entities through `DocumentLink` using entity type, entity ID, and link purpose.

Document archive:

- Is a soft operation.
- Retains the physical managed file.
- Retains metadata and ordinary links.
- Is blocked if the document is certificate-delivery evidence.

Restore sets status to Active when the file exists and Missing when it does not. `CheckMissingFilesAsync()` marks active records whose managed files cannot be found.

The Documents view can list all documents, show archived records, open files through Windows shell association, archive, restore, and refresh.

## 17. Reporting

`ReportService` supplies allocation-based reports for:

- Completed students.
- Withdrawn students with or without qualifying costs.
- Non-completions.
- Certificates awaiting order.
- Certificates awaiting delivery.
- Delivered certificates.

Completed, withdrawn, non-completion, and delivered reports support From and To date filters. The To date includes the complete selected calendar day. Reports can exclude archived students by default or include them explicitly.

CSV serialization is centralized in `ExportCsvAsync<T>()` and uses invariant culture and UTF-8 encoding.

## 18. Import and export

`ImportService` routes supported import formats.

Current imports:

- Migration package workbook.
- Completion-pricing/provider price-list CSV.
- Provider credit-history CSV.

Migration import includes legacy student-register detection and maps supported workbook data into the current relational model. Import services use the audit infrastructure and can place records requiring manual attention into review-oriented state where supported.

Current exports:

- Active course definitions CSV.
- Active students CSV.
- All allocations CSV.
- Completed/withdrawn/non-completion/certificate report CSVs.
- Invoicer billable batch export.

`InvoicerService` selects billable unexported allocations, creates export batches/items, and associates allocations with their export batch to prevent accidental duplicate export.

## 19. Backup and restore

`BackupService` creates ZIP backups containing the database and managed application data selected by the service. Backup files are stored under the configured backup location.

Restore replaces application data from a selected backup. Because restore changes persisted operational state, it should only be run when the user understands that the backup becomes the authoritative dataset.

## 20. Audit trail

`AuditService.Record()` creates `AuditLog` rows within the active DbContext. Services call `SaveChangesAsync()` after recording to persist the entry.

Common actions include:

- Created.
- Updated.
- Archived.
- Restored.
- ArchiveBlocked.
- Cancelled.
- CancellationBlocked.
- Attendance.
- Outcome.
- Transferred.
- Ordered.
- Delivered.
- CreditAllocated.
- CreditConsumed.
- CreditReleased.
- FundsAdded.
- CommitmentCreated.
- CommitmentReleased.
- ExpenseRecognised.
- Linked and Unlinked.

Audit entries identify entity type, internal ID, display ID where available, and optional before/after contextual data.

The audit database is the business-history record. Serilog files are the technical diagnostics record; the two mechanisms serve different purposes.

## 21. Diagnostic logging

Serilog writes daily files to:

`%LOCALAPPDATA%\StudentTracker\Logs\student-tracker-YYYYMMDD.log`

Configuration:

- Minimum level: Information.
- Daily rolling interval.
- 30-file retention.
- Shared file access.
- Flush to disk every two seconds.
- Timestamp, level, rendered message, and exception output.

Captured conditions include:

- Application startup.
- Demonstration-data seeding.
- Fatal startup failures.
- Unhandled AppDomain exceptions.
- Unhandled WPF dispatcher exceptions.
- Handled lifecycle/workflow exceptions shown through `DialogService.ShowError()`.

`Log.CloseAndFlush()` runs during normal application exit and startup-failure handling.

Do not write credentials, API keys, or document contents into logs or audit metadata.

## 22. Error-handling boundaries

Business-rule enforcement belongs in services. Examples include archive dependency checks, insufficient credit, certificate eligibility, duplicate allocation, and cancellation restrictions.

View models:

1. Ask for confirmation where an action changes lifecycle state.
2. Invoke the service.
3. Refresh bound collections after success.
4. Clear stale selections.
5. Catch expected operation failures at the command boundary.
6. Pass exceptions to `IDialogService.ShowError()` for logging and user feedback.

Unexpected dispatcher errors are captured globally as a final boundary.

## 23. UI automation

Main navigation buttons and destination headers expose stable automation IDs. FlaUI tests use UIA3 and an interactive Windows session to launch the WPF app and verify navigation and visible controls.

When adding controls:

- Preserve existing automation IDs.
- Add an automation ID when the control is part of a testable workflow.
- Prefer commands over logic in code-behind.
- Use code-behind only for UI gestures such as DataGrid double-click, then delegate to the view-model command.

## 24. Build and verification

Restore and build:

```powershell
dotnet restore StudentTracker.sln
dotnet build StudentTracker.sln -c Release
```

Unit tests:

```powershell
dotnet test tests\StudentTracker.Tests\StudentTracker.Tests.csproj -c Release
```

FlaUI tests:

```powershell
installer\publish.ps1
dotnet test tests\StudentTracker.UITests\StudentTracker.UITests.csproj -c Release
```

The UI tests require Windows and an interactive desktop session. Ensure no stale `StudentTracker` process is holding the Release output DLLs before rebuilding.

Recorded verification on 2026-08-29:

- Release solution build: passed with 0 warnings and 0 errors.
- Unit tests: 25 passed.
- FlaUI tests: 11 passed.

## 25. Publishing and release

The publish script is `installer\publish.ps1`.

```powershell
installer\publish.ps1
installer\publish.ps1 -Version 1.1.0
```

Output:

- `release\StudentTracker-win-x64\`
- `release\StudentTracker-win-x64-<version>.zip`

The product version is defined by `VersionPrefix` in `Directory.Build.props`. Keep `release\VERSION.txt` and release notes synchronized when preparing a release.

CI builds, tests, and performs a publish smoke check on Windows. Tagged `v*` releases publish the self-contained archive through the release workflow.

## 26. Extension guidance

When adding a new domain workflow:

1. Add or extend a model/enumeration in Core.
2. Add EF configuration or a migration when persistence changes.
3. Implement validation, state changes, audit records, and financial side effects in Services.
4. Add a view model using observable properties and relay commands.
5. Add a parameterless WPF view and bind it to the view model.
6. Register the service/view model in `App.ConfigureServices` if DI resolution is required.
7. Add a MainWindow data template for navigable or templated content where required.
8. Add confirmation and error handling for lifecycle-changing actions.
9. Add unit tests for business rules and FlaUI coverage for important user paths.
10. Run the Release build and both test projects.

For archive-like operations, prefer soft state changes over physical deletion, retain dependent history, provide restore where practical, guard active dependencies, and audit both successful and blocked attempts.

## 27. Known architectural considerations

- The application is designed for local single-user use; concurrent multi-user database access is not an established operating mode.
- Delivery operational status is currently represented as a string, while most other status families are enums. Converting it to an enum would require a migration and compatibility review.
- The root DI provider retains scoped services for the application lifetime. Introducing per-operation scopes would improve isolation if the application grows significantly.
- Current FlaUI coverage confirms navigation and baseline UI behavior but should be expanded for archive/restore confirmations, cancellation guards, certificate dialogs, and import/export file dialogs.
- Import preview/review tooling and custom report composition remain areas for future enhancement.
