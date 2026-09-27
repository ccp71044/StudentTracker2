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

## Real provider student and course lists

`ProviderListRealExportTests` imports the provider's student list and completed-course list and
checks the counts, the work groups, the parsed start dates and the review rows raised for the
rows that cannot be resolved automatically. They hold names, dates of birth and email addresses,
so they are not committed either.

Put your copies at `testdata/Student List (unique).xlsx` and
`testdata/Course List - Completed Allens.xlsx`, or point at them explicitly:

```
STUDENTTRACKER_STUDENT_LIST_WORKBOOK="C:\path\to\Student List (unique).xlsx" \
STUDENTTRACKER_COURSE_LIST_WORKBOOK="C:\path\to\Course List - Completed Allens.xlsx" dotnet test
```

The behaviour these exports exercise - blank surnames, `-` for an unknown date of birth,
non-breaking spaces, lower-case meridiems, truncated course names, one student under two clients
and near-identical surnames - is also covered by `ProviderListImportTests`, which builds its own
workbooks and therefore runs everywhere.
