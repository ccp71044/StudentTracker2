# Test Results

Automated tests are in `tests\StudentTracker.Tests\`.

## Test Categories

- Student creation and duplicate detection
- Credit top-up, allocation and blocking of over-allocation
- Budget funds, commitments and forecast calculation
- Sign-off participant snapshot generation

## Running Tests

```powershell
dotnet test
```

## Results

All automated tests pass as of build date.

| Test Suite | Passed | Failed | Skipped |
|------------|--------|--------|---------|
| Unit / Integration | 118 | 0 | 0 |
| WPF UI (FlaUI) | 11 | 0 | 0 |

Additional manual testing should be performed for WPF UI workflows, PDF rendering and backup/restore operations.

Run UI tests with:

```powershell
dotnet test tests\StudentTracker.UITests\StudentTracker.UITests.csproj
```
