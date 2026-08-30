# Next Task: V2 Spec Gap Remediation Plan

**Date:** 2026-08-30  
**Source:** `Student_Tracker_Comprehensive_Program_Design_and_Test_Plan_v2.md`  
**Status:** This is the active next task for the project.

---

## 1. Goal

Close the remaining high-priority gaps between the current Student Tracker 2 build and the v2 comprehensive specification, with a focus on the multi-pool prepaid client-place workflow, the Allens Cost vs client charge split, and the Invoicer information-sharing requirements.

---

## 2. Verified Foundation (do not regress)

- .NET 8 WPF, SQLite, EF Core, MVVM, Serilog, QuestPDF, xUnit.
- Students, courses, deliveries, allocations, documents, budgets, credits, reports, settings.
- Sign-off PDF generation, certificate order/delivery, audit log, backup/restore.
- Release build `StudentTracker.exe` and `release/StudentTracker-win-x64-1.0.0.zip`.
- Unit tests 99/99 passing, FlaUI navigation tests passing.

---

## 3. Gaps to Remediate (in priority order)

### Phase 1 — Client Prepaid Entitlement Ledger (highest priority)

The v2 spec requires a **quantity-based** client-prepaid entitlement ledger separate from the cash budget. Current `BudgetPool`/`BudgetTransaction` is cash only.

Tasks:

1. Add `ClientPrepaidEntitlement` domain:
   - Model: `ClientPrepaidEntitlementTransaction` with `PoolId`, `Client`, `CourseDefinitionId`/`CourseCategory` restriction, `Quantity`, `TransactionType` (PrepaidPlacesAdded, PlaceReserved, PlaceAssigned, PlaceReleased, PlaceConsumed, PlaceTransferred, PlaceAdjustment, PlaceReversal), `SourceInvoice/Reference`, `AllocationId`, `LinkedTransactionId`, `Reason`, `Notes`.
   - EF Core migration.
2. Add service `ClientPrepaidEntitlementService`:
   - `AddPrepaidPlacesAsync`
   - `ReservePlaceAsync`
   - `AssignPlaceAsync`
   - `ReleasePlaceAsync`
   - `ConsumePlaceAsync`
   - `TransferPlaceAsync`
   - `GetPoolPositionAsync` returning loaded/consumed/reserved/placeholder/unassigned carry-forward/additional funding requirement/forecast carry-forward.
3. Add `ClientPrepaidPool` model or extend `BudgetPool` with a `PoolKind` (Cash vs ClientPrepaid). Prefer distinct pool table to avoid conflating ledgers.
4. Add display IDs: `CPP-0001`, `CPT-0001`.
5. Unit tests for all transaction types and the calculations in 10A.6.

Exit criteria:
- `MP-002` through `MP-007` and `MP-013` can be demonstrated with passing tests.

---

### Phase 2 — Allens Cost Snapshot Fields

The v2 spec requires the internal certificate cost to be tracked separately from the invoice/client charge and snapshot at allocation.

Tasks:

1. Rename/extend `CourseDefinition.DefaultCertificateCost`:
   - Keep `DefaultCertificateCost` but add `DefaultAllensCost` where it differs.
2. Add `Allocation` fields:
   - `AllensCostAtAllocation` (decimal?)
   - `ActualAllensCost` (decimal?)
   - `ClientPrepaidPoolId` (Guid?)
   - `ClientPrepaidEntitlementTransactionId` (Guid?)
3. EF Core migration.
4. Update allocation creation/editing to capture `AllensCostAtAllocation` from course default at the time of allocation.
5. Update cost-spent workflow to record `ActualAllensCost`.
6. Unit tests for `MP-014` (historical cost snapshot not overwritten when default changes).

Exit criteria:
- Old allocations keep their historical Allens cost when course default is changed.

---

### Phase 3 — Pool Position Dashboard

Create the single-screen dashboard from spec section 10C.

Tasks:

1. New `PoolPositionView` / `PoolPositionViewModel` accessible from main navigation and/or Reports.
2. Select client/course pool.
3. Display:
   - Prepaid places loaded
   - Completed/consumed
   - Reserved to named students
   - Reserved placeholders
   - Unassigned carry-forward
   - Current requested students
   - Covered by carry-forward
   - Additional places requiring funding
   - Forecast carry-forward
   - Allens Cost committed / actually incurred / forecast
   - Latest invoice/reference
   - Invoice/payment status from Invoicer reference
4. Display certificate credits available/allocated/consumed separately.
5. Export to CSV/JSON for Invoicer reference.

Exit criteria:
- User can answer all 20 questions in spec section 28A directly from the dashboard without manual spreadsheet reconstruction.

---

### Phase 4 — Invoicer Import and Reference Panels

Complete the bidirectional, read-only file exchange.

Tasks:

1. Create `Integration/InvoicerToStudentTracker/` watch/import handler.
2. Import `Invoice` reference data idempotently:
   - External Invoice ID, Invoice Number, Customer, Invoice Date, Total, Payment Status, Amount assigned to Student Tracker, PDF path.
3. Add `InvoicerReferencePanel` to `PoolPositionView`/`CreditsBudgetsView` showing:
   - Latest relevant invoice
   - Invoice number/date
   - Payment status
   - Quantity funded
   - Value for reference
   - Last synchronised time
4. Update `InvoicerReferenceExportService` to include pool position data (requested/carry-forward/additional places) if not already present.
5. Add service-level tests for idempotent import and duplicate prevention (`MP-011`).

Exit criteria:
- Exporting the same batch twice does not duplicate records.
- Importing an Invoicer reference file twice is idempotent.

---

### Phase 5 — Reports and Workflow Test Coverage

Expose remaining mandatory reports and encode the v2 workflow tests.

Tasks:

1. Add UI entries for missing reports in section 14 of the spec, prioritising:
   - Withdrawn Students / with/without Costs
   - Non-Completions
   - Credits Consumed Without Completion
   - Certificates Awaiting Order / Ordered / Awaiting Delivery / Delivered
   - Certificate Credit Pool Summary
   - Credit Transaction History
   - Credit Reallocation History
   - Budget Summary / Pending Commitments / Actual vs Forecast
   - Funding Sources
   - Missing Documents
   - TBC Course Deliveries
   - Billable Certificates for Invoicer
   - Audit Activity
2. Add unit/integration tests for each report query.
3. Add workflow tests `WF-001` through `WF-010` and `MP-001` through `MP-014`.
4. Update `TEST_RESULTS.md` as tests pass.

Exit criteria:
- All 25 mandatory reports are reachable in the UI.
- Each major workflow from section 23 and 23A has at least one passing automated test.

---

## 4. Rollout Order

Execute strictly in phase order:

1. Phase 1: client prepaid entitlement ledger.
2. Phase 2: Allens Cost fields.
3. Phase 3: pool position dashboard.
4. Phase 4: Invoicer import/reference panels.
5. Phase 5: reports and workflow tests.

After each phase:
- build release;
- run unit tests (target: 0 warnings, all pass);
- run FlaUI tests;
- republish `release/StudentTracker-win-x64`;
- commit and push.

---

## 5. Out-of-Scope (for now)

- Custom report builder.
- UI polish/inline editing.
- Advanced performance testing beyond existing 10k-allocation targets.
- Cloud or multi-user features.

---

*This document is the active next task. The first actionable sub-task is Phase 1, Task 1: add the `ClientPrepaidEntitlementTransaction` model and migration.*
