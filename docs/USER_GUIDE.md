# Student Tracker User Guide

## Main Navigation

Use the sidebar buttons to switch between:

- **Dashboard** – summary counts, budget pool position, completions remaining, and reconciliation status
- **Students** – student register and search
- **Courses** – course definitions
- **Deliveries** – scheduled course deliveries
- **Allocations** – student-to-delivery allocations and placeholder positions
- **Certificates** – certificate ordering and delivery
- **Credits & Budgets** – certificate-credit pools and cash-budget pools
- **Documents** – linked document management
- **Reports** – operational and control reports with CSV export
- **Import / Export** – migration import, data exports, and Invoice Manager snapshots
- **Settings** – application configuration

The same navigation areas are also available from the **View** menu in the menu bar.

## Menu Bar

The conventional menu bar at the top of the content area provides:

| Menu | Contents |
|---|---|
| **File** | Import Migration Package, Backup Now, Restore Backup, Exit |
| **Actions** | Refresh Current View (F5) |
| **Data** | Backup Now, Restore Backup, Replace All Data from Migration Package, Compact Database |
| **View** | Dashboard, Students, Courses, Deliveries, Allocations, Certificates, Credits & Budgets, Documents, Reports, Import / Export, Settings |
| **Tools** | Open Data Folder, Open Backups Folder, Open Exports Folder, Open Logs Folder, Compact Database |
| **Help** | Documentation, About |

## Adding a Student

1. Go to the **Students** view.
2. Click **Add Student**.
3. Edit the student details and save.

## Adding a Course

1. Go to the **Courses** view.
2. Click **Add Course**.
3. Enter the course code and title.

## Creating a Delivery

Use the **Deliveries** view or the course detail to create a scheduled delivery. Dates can be confirmed, estimated, TBC or blank.

## Allocating a Student to a Delivery

Open the delivery, then use the allocation workflow to select a student, set cost, credit pool and budget pool.

## Budget Pools and the Prepaid-Place Model

Budget pools represent pre-purchased blocks of training credit. Each pool tracks funds added, committed (reserved for a pending allocation), and spent (cost recognised after completion).

### Personal vs client pools

Every budget pool has a **category** (visible in the pool editor):

- **Personal / internal funds** – spending from the business's own budget.
- **Client-funded** – spending on behalf of a specific client account (the pool's `ClientName` field identifies the client).
- **Other** – uncategorised.

Well-known pool names in use are **SCJV** (client-funded), **General** (personal/internal), and **Allens Training Credit** (provider credit mirror).

### Allen / provider cost vs client charge

The **Allen cost** (also called the provider completion price) is the price the training provider charges per completion. It is sourced from the course price list and determines how many more completions the pool can fund. The **certificate cost** on an allocation is the amount committed or spent against the budget pool for that student's place and may differ from the provider cost where negotiated rates or client-specific pricing apply.

The Dashboard and Credits & Budgets views show **Allen cost** and **Completions remaining** per pool and course so you can see at a glance how many more students can be put through before the pool needs topping up.

### Manual spend and reversal actions

The Allocations view toolbar and context menu provide four budget actions on the selected allocation:

1. **Create/Restore Commitment** – reserve budget funds for the allocation's certificate cost. Available when the allocation has a linked budget pool and no active commitment.
2. **Release Commitment** – release reserved funds without recognising the expense. Available when a commitment is pending.
3. **Mark Cost Spent** – recognise the committed amount as actual expenditure. Available when a commitment is pending. If the allocation outcome is not yet Completed, a confirmation prompt warns that spending ahead of completion is unusual and requires explicit confirmation.
4. **Reverse Spent Cost** – undo a spent cost and return the allocation to Released status, returning the funds to the pool. Available when cash commitment status is Spent.

Each action creates audited budget transactions and updates the allocation's cash commitment status. These are manual, deliberate actions; there is no automatic spending on completion.

## Position Dashboard

The **Dashboard** view shows:

- Summary counts for students, active courses, deliveries, allocations, and certificate orders.
- A **Budget pools** table showing each pool's funds added, committed, spent, available balance, unassigned placeholder places, assigned pending places, and completed allocations awaiting manual spend.
- A warning banner if any pool's available balance is negative.
- A **Completions remaining** table showing how many more completions of each course the free balance covers, calculated at the current Allen cost per completion.
- A **reconciliation status** comparing the register's recorded top-ups against the provider's credit-purchase history. Discrepancies are counted and the net difference is shown.

The same pool and completions-remaining tables appear in the **Credits & Budgets** view's Budget Pools tab.

## Marking Outcomes

For each allocation you can mark attendance, completion, withdrawal or non-completion. Withdrawal requires a reason.

## Certificates and Credits

Allocate certificate credits from a pool, order certificates, and record delivery. Credit balances are calculated from transactions and cannot be edited directly.

## Sign-Offs

Generate a Course Delivery Completion Sign-Off PDF from a delivery. The PDF lists participants, delivery dates and signatory blocks.

## Backups

Backups can be created from the application and include the SQLite database, documents and templates. Backups are also available from **File → Backup Now** and **Data → Backup Now** in the menu bar.

## Replace All Data (Guided Cutover)

**Data → Replace All Data from Migration Package** performs a destructive, guided replacement of the entire database from a canonical migration workbook. This is designed for the initial production cutover — loading the authoritative dataset into a clean database — and is not intended for routine use.

The guided workflow:

1. Select the canonical migration-package Excel workbook.
2. The workbook is validated: required sheets and columns, no duplicate identifiers, no broken relationships, valid enum values.
3. A preview dialog shows current database counts alongside workbook counts and warns that the operation is destructive.
4. Type `REPLACE DATA` exactly to confirm.
5. A verified backup is created before any changes.
6. The workbook is re-validated immediately before the destructive operation to guard against file changes since the preview.
7. A SQLite `PRAGMA integrity_check` is run.
8. All operational data is deleted inside a database transaction (app settings and EF migration history are preserved; document files are never touched).
9. The workbook is imported using the standard migration importer.
10. Imported counts are reconciled against the workbook; broken relationships are checked.
11. The transaction is committed and a verified post-cutover backup is created.

If any step fails, the transaction is rolled back and no database changes are committed. Both the pre-cutover and post-cutover backup paths are displayed on success.

**Important:** the live production cutover has not yet been run. The workbook has been validated but the authoritative dataset has not yet been loaded into the live application database.

## Import / Export and Invoice Manager Reference Exchange

The **Import / Export** view lets you back up, restore, export register data to CSV and produce Invoice Manager reference snapshots.

### Invoice Manager Cost Position Snapshot

Use **Export Invoice Manager Cost Position** to write a read-only, versioned JSON and CSV snapshot of the current budget-pool cost position to `Integration/InvoicerExport`.

Student Tracker remains the source of truth for completions, pending commitments and budget pool balances. The snapshot is a reference exchange only:

- It does not create shared database access.
- It does not grant invoice or payment authority.
- It does not modify allocations, commitments or the general ledger.

Each snapshot file name includes a UTC timestamp and a counter suffix if the same second is exported more than once, so files are never overwritten accidentally. The JSON snapshot contains:

- `SchemaVersion` and `SnapshotId` for stable reference.
- `GeneratedAt` timestamp.
- Per-pool totals: funds added, committed, spent, available, anonymous reserved places, assigned pending, completed awaiting manual spend and completions remaining.
- Per-course details: course code/title/provider, funds, committed, spent, available, anonymous reserved places, assigned pending, completed awaiting manual spend, completions remaining, provider (Allen) cost and total allocations.

The same export is also available from the **Reports** view as **Export Invoice Manager Cost Position**.
