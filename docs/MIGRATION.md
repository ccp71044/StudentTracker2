# Migration Guide

Student Tracker supports importing historical data from the supplied workbook:

`Student_Tracker_Complete_Migration_Package.xlsx`

## Import Wizard

1. Create a backup before importing.
2. Open the Imports/Exports view and select the migration package.
3. Review the staged records and approve or correct inferred values.
4. Run the import. The operation is transactional; if it fails, the database is rolled back.

## Data-Preservation Rules

- Original workbook rows are never deleted.
- External transaction IDs, course numbers, purchase references and legacy SCJV references are retained.
- Uncertain records are placed in the review queue.
- Blank dates become TBC.
- SCJV PENDING rows become placeholder allocations.

## Reconciliation

After import, run the reconciliation report to compare provider credit totals against imported transaction totals.
