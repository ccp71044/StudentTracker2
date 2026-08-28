# Tests

```
dotnet test tests/StudentTracker.Tests/StudentTracker.Tests.csproj
```

Most tests are self-contained and run anywhere. `StudentTracker.UITests` drives the built
application through FlaUI and therefore only runs on Windows.

## Real register workbook

`LegacyImportTests` exercises the legacy importer against the real `Student Tracker.xlsx`
register. That workbook holds student names, email addresses and phone numbers, so it is
**not** committed and `testdata/` is git-ignored.

Put your copy at `testdata/Student Tracker.xlsx` in the repository root, or point at it
explicitly:

```
STUDENTTRACKER_REGISTER_WORKBOOK="C:\path\to\Student Tracker.xlsx" dotnet test
```

Without it the test reports that it was skipped and passes, so CI and contributors without
the register are unaffected.

## Real provider credit export

`ProviderCreditHistoryRealExportTests` imports the provider's credit transaction export and
checks that every row lands in the ledger and that the derived balance equals credits minus
debits. The export names the staff member responsible for each purchase, so it is not
committed either.

Put your copy at `testdata/credit-transaction-history-<date>.csv` (the newest matching file
is used), or point at it explicitly:

```
STUDENTTRACKER_CREDIT_HISTORY_CSV="C:\path\to\credit-transaction-history.csv" dotnet test
```
