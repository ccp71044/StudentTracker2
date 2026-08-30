# Student Tracker 2 — Progress, Gaps and Next Steps

**Date:** 2026-08-30  
**Current branch:** `main`  
**Git status:** in sync with `origin/main`  

## 1. What has been completed

### Core feature integration
- Receipt / credit top-up workflow with managed documents and transaction history.
- Issued-certificate evidence storage and document linking.
- Records-of-completion (sign-off) draft PDF generation, signed-PDF import, locking and versioning.
- Budget pool and certificate-credit pool workflows.
- Prepaid-place reservation and carry-forward, manual cost-spend and reversal.
- Invoice Manager exchange (file-based JSON/CSV snapshots).
- Guided destructive data cutover (completed 2026-08-30).
- Authoritative migration workbook loaded into live database.
- Reports expanded and UI simplified:
  - 22 reports grouped by category (Students, Certificates, Deliveries, Allocations, Financial, Administration).
  - Single report content area replacing the 22-tab layout.
  - CSV export for each report.
- Main window menu bar added: `File`, `Actions`, `Data`, `View`, `Tools`, `Help`.
- Row-level right-click context menus added to core tables.
- Keyboard and double-click navigation retained where present.

### Testing and build
- Release build: `0 errors`.
- Unit tests: `97 passed`.
- FlaUI tests: `58/58 passed` (serial execution).
- Published executable regenerated:
  - `release\StudentTracker-win-x64\StudentTracker.exe`
  - `release\StudentTracker-win-x64-1.0.0.zip`

### Live database status
- **Students:** 32
- **Course definitions:** 23
- **Course deliveries:** 37
- **Allocations:** 53 (re-sequenced to `ALL-0001` → `ALL-0053`)
- **Budget pools:** 2 (`BUD-0001 Personal`, `BUD-0002 T&C / client funded`)
- **Certificate-credit pools:** 1
- **Import-review issues:** 0
- **Audit logs:** present
- **SQLite integrity:** `ok`
- **Backup created:** `StudentTracker-backup-20260830-110526-manual-budget-pools.zip`

### Bug fixes
- `DisplayIdGenerator.NextDisplayId` was parsing `BUD-0001` / `ALL-0001` as `-1`, causing all generated IDs to remain at `-0001`. Fixed by trimming the leading dash and only accepting positive numbers.
- Duplicate `BudgetPool` and `Allocation` display IDs re-sequenced in the live database.

## 2. Remaining gaps

### Operational data still needed
1. **Budget pool opening balances** are currently `$0`.
   - Personal pool needs actual opening funds.
   - T&C/client-funded pool needs actual opening funds.
2. **Allen/provider course prices** need review and confirmation in the course definitions.
3. **Prepaid places** still need to be reserved against the appropriate funded pool(s) and course deliveries.
4. **Signed completion PDF** (`SCJV Course Signoff 250826 tb signed.pdf`) still needs to be imported through the Records of Completion workflow and linked to the relevant delivery, sign-off, allocations and students.
5. **Real certificate evidence and receipts** should be loaded as managed documents when they arrive.

### Functional coverage not yet fully exercised against live data
- One student doing multiple courses end-to-end.
- Placeholder allocation replacement and transfer.
- Manual cost-spend and reversal.
- Certificate ordering and delivery with evidence.
- Record-of-completion generation, wet-signature import, and lock-as-signed.
- Receipt top-up and retrieval from transaction history.
- Backup/restore round-trip on the live data set.

### Test and code-quality debt
- Older xUnit1031 warnings in blocking test methods remain pre-existing; converting to `async Task` would clean the build warnings.
- Additional FlaUI coverage would be useful for File/Actions menus, context menus, receipt dialog, certificate-evidence selection, records-of-completion dialog, signed-PDF import, and data cutover preview.
- Stronger duplicate-allocation validation if the business rule requires one allocation per student per delivery.
- Persistent import-review UI if review queue items need resolution beyond the import-result display.

## 3. Recommended next steps

### Immediate (data entry)
1. Enter real opening balances for the `Personal` and `T&C / client funded` budget pools.
2. Confirm and enter actual course prices for Allen/provider deliveries.
3. Reserve prepaid student places against the newly funded pools.
4. Import the provided wet-signed completion PDF and lock the sign-off.

### Near term (verification)
5. Exercise the full certificate-ordering/delivery/evidence workflow on a real or test allocation.
6. Generate a record-of-completion draft, import the signed copy, and verify the final PDF opens from the sign-off screen.
7. Run a round of real financial reports (Budget Summary, Credit Summary, Certificate Orders) and export to CSV.

### Quality improvements
8. Convert the remaining xUnit1031 warnings to `async Task` tests.
9. Add the missing FlaUI coverage noted above.
10. Re-evaluate whether `BudgetTransaction` and `CertificateCreditTransaction` display-IDs need historical re-sequencing if transactions are created in bulk before the `DisplayIdGenerator` fix.

## 4. Key files and locations

- Live database: `%LOCALAPPDATA%\StudentTracker\Database\student-tracker.db`
- Backups: `%LOCALAPPDATA%\StudentTracker\Backups\`
- Logs: `%LOCALAPPDATA%\StudentTracker\Logs\`
- Release executable: `release\StudentTracker-win-x64\StudentTracker.exe`
- Release zip: `release\StudentTracker-win-x64-1.0.0.zip`
- Technical docs: `docs/TECHNICAL_REFERENCE.md`, `docs/USER_GUIDE.md`, `docs/FUNCTIONALITY_ANALYSIS.md`

---

*Generated with [Devin](https://devin.ai)*
