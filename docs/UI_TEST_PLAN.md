# Offline UI Test Plan

## Tooling

Playwright drives browsers over the Chrome DevTools/WebDriver BiDi protocols and cannot see a WPF
window, so it is not usable here. The Windows equivalents are:

| Tool | Verdict |
| --- | --- |
| **FlaUI (UIA3)** | **Chosen.** Native .NET library over UI Automation, runs in-process with xUnit, already present in `tests/StudentTracker.UITests`. |
| WinAppDriver | Appium-flavoured alternative. Microsoft has effectively stopped maintaining it; adds a server process for no gain. |
| Playwright | Browser only. Revisit only if the UI is ever ported to web. |

Everything runs offline: no network calls, no external services, no signed-in accounts. The app is
launched from a locally published build against a throwaway data directory.

## Environment

Run on Windows 10/11 with an interactive desktop session (UI Automation needs a real desktop; a
locked or headless session fails).

```powershell
dotnet build StudentTracker.sln -c Release
dotnet test tests/StudentTracker.UITests/StudentTracker.UITests.csproj -c Release
```

`AppUiTestFixture` already starts `StudentTracker.Wpf.exe --sample-data` with `LOCALAPPDATA`
redirected to a temporary folder, so each run gets its own database, documents, logs and backups and
deletes them afterwards. Nothing touches the operator's real `%LOCALAPPDATA%\StudentTracker`.

## Prerequisite: automation IDs

Only the navigation buttons and view headers carry `AutomationProperties.AutomationId` today. Every
control a test touches needs a stable ID, because matching on button text breaks whenever a label is
reworded and matching on tree position breaks whenever a panel is rearranged.

Naming convention: `<Screen><Control><Kind>`, e.g. `StudentsAddButton`, `StudentEditFirstNameBox`,
`StudentsErrorText`, `StudentsGrid`.

IDs required before the suites below can be written:

- every `Button` in `Views/*.xaml` and `MainWindow.xaml`;
- every input in the edit dialogs (student, course, budget pool, add funds);
- every `DataGrid` (`...Grid`) so row counts and selection can be asserted;
- the `ErrorMessage` / `Status` text blocks on each screen;
- the dialog window itself (`DialogWindow`), so a test can assert it opened and closed.

## Suites

### 1. Smoke (must pass before anything else runs)

- App starts, main window titled "Student Tracker" appears within 30s.
- Each of the 11 navigation buttons shows its section header — already covered by `NavigationTests`.
- No error text is visible on any screen after a cold start with sample data.
- `student-tracker-*.log` in the test data directory contains the startup line and no `Error` or
  `Fatal` entries.

### 2. Every button does something

One test per button. The pattern is: note the observable state, invoke the button, assert the state
changed **or** a specific message appeared. A button that leaves the screen unchanged and writes no
message fails the test — that is exactly the failure being guarded against.

| Screen | Button | Expected observable effect |
| --- | --- | --- |
| Dashboard | Refresh | Counts repopulate after the underlying data changes |
| Dashboard | Add Student / Add Course | Edit dialog opens; cancelling leaves counts unchanged |
| Students | Search | Grid narrows to matches; clearing restores the full list |
| Students | Add / Edit / View / Archive | Dialog opens; Edit/View/Archive disabled with no selection, enabled with one |
| Courses | Add / Edit / Delete | As above; Delete deactivates and drops the row from the list |
| Deliveries, Allocations, Certificates | Refresh | Grid reloads, row count stable |
| Credits & Budgets | Add Pool / Edit / Add Funds / Archive | Pool appears, balance increases by the amount added, archived pool disappears |
| Documents | Add Document | File picker opens; a chosen file appears in the grid |
| Reports | Export Completed CSV | File written to the chosen path and is non-empty |
| Import/Export | Create Backup | Zip appears in the backups folder; `Status` names it |
| Import/Export | Restore Backup | Data returns to the backed-up state |
| Import/Export | Export for Invoicer | Batch file written, or "No billable items to export." |
| Import/Export | Import Workbook / price list / credit history | Row counts increase; `Status` names the format detected and the result |
| Settings | Compact Database | "Database compacted." and the database file still opens |

File dialogs (`OpenFileDialog`/`SaveFileDialog`) are OS windows, not part of the app's visual tree.
Drive them through the fixture: find the dialog by class name `#32770`, type the path into the
`Edit` control, invoke `Open`/`Save`. Fixture files live under `tests/fixtures/`.

### 3. Validation and failure messages

Each of these must show a specific message and must not close the dialog or crash:

- Save a student with a blank first or last name → "First name and last name are required."
- Save a course with no code or title → "Course code and title are required."
- Add funds of `0` or a negative amount → "Enter an amount greater than zero."
- Import a CSV that is not a provider export → the missing-column message.
- Import a corrupt `.xlsx` → an import-failed message.
- Restore a backup zip that does not exist → a restore-failed message.

After each, assert the matching failure was written to `student-tracker-*.log`. Error logging is new
in this release, so it is worth asserting rather than assuming.

### 4. Workflow scenarios (WF-001 … WF-010 from the design plan)

Each runs end-to-end through the UI on a fresh sample-data instance and asserts both the on-screen
result and the resulting ledger figures:

1. WF-001 allocate a student to a delivery, reserving credit.
2. WF-002 complete a course and recognise the expense.
3. WF-003 withdraw a student with a reason, credit reusable.
4. WF-004 withdraw a student, credit lost.
5. WF-005 record a non-completion.
6. WF-006 order a certificate, consuming reserved credit.
7. WF-007 attempt a duplicate order → blocked with a message.
8. WF-008 order a replacement certificate with a reason.
9. WF-009 deliver a certificate and mark it billable.
10. WF-010 export the billable items for Invoicer.

Balances are asserted on the Credits & Budgets screen: available credit, actual expenditure and
forecast available must match the ledger arithmetic in the design plan, not just "a number changed".

### 5. Data-heavy behaviour

The realistic run loads the provider's own exports through **Import/Export → Import Workbook**, in
this order, and checks each screen afterwards:

1. `Student List (unique).xlsx` → 35 students on the Students screen, 36 rows in, one provider
   number appearing under two clients.
2. `Course List - Completed Allens.xlsx` → 35 deliveries, 24 courses, every start date populated.
3. `credit-transaction-history-<date>.csv` → credit pool available balance 82.
4. `Student Tracker.xlsx` (the legacy register) → the allocations and budget history.

Assertions specific to this data:

- The Students grid shows the two students with no surname rather than dropping them.
- The three near-identical "Prince" records are all present and flagged as potential duplicates —
  never merged.
- The review queue holds the rows the import could not resolve (the truncated course set, the
  student under two clients, the missing surnames) rather than silently importing them.
- Re-importing any of the four files adds nothing the second time.
- Load a database with ~5,000 allocations and assert each screen renders within 3 seconds.

These files name real people, so they live in the git-ignored `testdata/` folder (see
`tests/README.md`) and the tests skip when they are absent.

## Evidence and triage

- Capture a screenshot on every failure (`window.Capture().ToFile(...)`) and publish it with the
  `.trx`.
- Copy the run's `Logs` folder into the test output on failure; the app log usually names the cause
  outright now.
- Record the app version from Settings in the run output so a failure can be pinned to a build.

## Running it in CI

Add a second job to `.github/workflows/ci.yml`, after the existing build, on `windows-latest`
(GitHub's Windows runners have an interactive desktop, so UI Automation works):

```yaml
  ui-tests:
    runs-on: windows-latest
    needs: build
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet build StudentTracker.sln -c Release
      - run: dotnet test tests/StudentTracker.UITests/StudentTracker.UITests.csproj -c Release --logger "trx"
      - uses: actions/upload-artifact@v4
        if: always()
        with: { name: ui-test-results, path: '**/TestResults/**' }
```

UI tests are slower and flakier than the service tests, so keep them in their own job: a red UI job
should be readable as "a screen broke", not as "the build broke".

## What stays outside the UI suite

Ledger arithmetic, importers, report contents, document checksums and backup internals are covered
by `tests/StudentTracker.Tests` and run on any OS in seconds. Only add a UI test when the thing being
verified is the screen itself — clicking through to re-test arithmetic makes the suite slow without
making it stronger.

## Sequencing

1. Add the automation IDs listed above (mechanical, one pass over the XAML).
2. Extend the fixture with file-dialog handling, screenshot-on-failure and log assertions.
3. Suite 2 (every button), then suite 3 (validation) — these catch the reported class of bug.
4. Suite 4 (workflows), which needs the delivery-detail and outcome screens that are not built yet.
5. Suite 5 and the CI job.
