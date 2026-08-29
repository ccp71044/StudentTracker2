# Data Migration Format Specification

Use this document when preparing a migration workbook for Student Tracker 2.

The import expects a single Excel workbook (`*.xlsx`) with separate sheets, one per entity. Use the exact sheet and column names below. Extra columns are ignored; missing required values may cause rows to be skipped.

---

## General formatting rules

1. Use the exact sheet and column names shown below.
2. One header row only, starting in cell `A1` of each sheet.
3. Do not merge cells or use formulas.
4. Dates must be in `dd/MM/yyyy` format.
5. Numbers must not contain currency symbols or thousands separators — for example, `25.00`.
6. Booleans must be `TRUE` or `FALSE`.
7. Text fields should not contain leading or trailing spaces.
8. Optional columns may be left blank.

---

## Sheet 1: `Students`

| Column | Type | Example | Required |
|---|---|---|---|
| DisplayId | text | STU-001 | No |
| FirstName | text | Jane | Yes |
| LastName | text | Smith | Yes |
| Email | text | jane.smith@example.com | No |
| Phone | text | 07700 123456 | No |
| Employer | text | ABC Care Ltd | No |
| WorkGroup | text | North Region | No |
| Manager | text | Tom Brown | No |
| GroupTag | text | 2024-Cohort | No |
| IsActive | TRUE/FALSE | TRUE | No (defaults TRUE) |
| IsArchived | TRUE/FALSE | FALSE | No (defaults FALSE) |

---

## Sheet 2: `CourseDefinitions`

| Column | Type | Example | Required |
|---|---|---|---|
| CourseCode | text | MHFA-001 | Yes |
| CourseTitle | text | Mental Health First Aid | Yes |
| Category | text | Health | No |
| Provider | text | MHFA England | No |
| DefaultCertificateCost | number | 25.00 | No |
| CourseDurationDays | integer | 2 | No |
| IsActive | TRUE/FALSE | TRUE | No (defaults TRUE) |

---

## Sheet 3: `CourseDeliveries`

| Column | Type | Example | Required |
|---|---|---|---|
| CourseCode | text | MHFA-001 | Yes |
| DisplayId | text | DEL-001 | No |
| StartDate | date (dd/MM/yyyy) | 15/09/2025 | No |
| EndDate | date (dd/MM/yyyy) | 16/09/2025 | No |
| DateStatus | Confirmed / Estimated / TBC / Blank | Confirmed | No |
| Location | text | Manchester Training Room | No |
| TrainerName | text | Sarah Jones | No |
| TrainerBusinessDetails | text | SJ Training | No |
| Capacity | integer | 12 | No |
| DeliveryStatus | Scheduled / Cancelled / Completed | Scheduled | No (defaults Scheduled) |
| Notes | text | Morning session only | No |

---

## Sheet 4: `Allocations`

| Column | Type | Example | Required |
|---|---|---|---|
| StudentDisplayId | text | STU-001 | Yes |
| DeliveryDisplayId | text | DEL-001 | Yes |
| AllocationStatus | Reserved / Enrolled / Active / Transferred / Withdrawn / Finalised / Cancelled | Enrolled | No (defaults Enrolled) |
| AttendanceStatus | NotRecorded / Confirmed / Attended / PartiallyAttended / DidNotAttend / Exempt | Attended | No (defaults NotRecorded) |
| OutcomeStatus | Pending / Completed / NotCompleted / Withdrawn / Transferred / Cancelled | Completed | No (defaults Pending) |
| OutcomeDate | date (dd/MM/yyyy) | 16/09/2025 | No |
| CertificateCost | number | 25.00 | No (falls back to course default) |
| BudgetPoolName | text | 2024-25 Budget | No |
| CreditPoolName | text | 2024 Credits | No |

---

## Optional: `BudgetPools`

| Column | Type | Example | Required |
|---|---|---|---|
| Name | text | 2024-25 Budget | Yes |
| FinancialPeriod | text | 2024-25 | No |
| Notes | text | Main annual budget | No |
| IsActive | TRUE/FALSE | TRUE | No (defaults TRUE) |

---

## Optional: `CertificateCreditPools`

| Column | Type | Example | Required |
|---|---|---|---|
| Name | text | 2024 Credits | Yes |
| Provider | text | Highfield | No |
| UnitType | Monetary / Count | Count | No (defaults Count) |
| ExpiryDate | date (dd/MM/yyyy) | 31/12/2025 | No |
| Notes | text | Annual certificate credit | No |
| IsActive | TRUE/FALSE | TRUE | No (defaults TRUE) |

---

## Import process

1. Save the workbook using the exact sheet and column names above.
2. Open Student Tracker 2.
3. Go to **Import / Export**.
4. Click **Import Migration Package** and select the workbook.
5. Review the import result message.
6. Check the Students, Courses, Deliveries, and Allocations screens to confirm the data loaded correctly.

If a row cannot be imported, the importer will report the problem in the status message.

## Guided full-data cutover

Use **Data > Replace All Data from Migration Package** (also available on **Import / Export**) only for an approved full replacement. This is deliberately separate from the additive **Import Migration Package** command.

Before enabling confirmation, the workflow reads the workbook without changing the database and reports current database and workbook counts. It requires the four canonical sheets and their headers/required values; checks duplicate student, course and delivery identifiers and pool names; validates course/delivery/student/pool references and supported enum values; and rejects unreadable or invalid packages.

To proceed, type `REPLACE DATA` exactly in the warning dialog. The application then revalidates the package, checks database integrity, and creates and verifies a `verified-pre-cutover` backup. Inside one database transaction it clears operational and test records, imports via `MigrationPackageImporter`, rejects any import error or review-queue item, and reconciles exact counts and course/student/delivery relationships. Failure rolls back the transaction and clears EF's change tracker so deleted/imported tracked objects cannot leak into later saves. Success writes a `DataCutoverCompleted` audit record, commits, then creates and verifies a `verified-post-cutover` backup.

The replacement removes database rows for students, courses/prices/deliveries, allocations, pools and transactions, funding/invoices/certificates/sign-offs, document metadata/links, imports/exports, outcome reasons and prior audit records. It preserves `AppSettings` and `__EFMigrationsHistory`. **It never deletes files from the Documents directory.** The pre-cutover backup includes the database, document files, and templates.
