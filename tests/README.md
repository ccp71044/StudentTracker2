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
