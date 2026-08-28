# Lifecycle Workflow Task List — 2026-08-29

## Safety and consistency

- [x] Add confirmation prompts before archive, restore, cancel, and document lifecycle actions.
- [x] Standardise soft-removal terminology as **Archive** and expose **Restore** where supported.
- [x] Add dependency checks so records with active operational dependencies cannot be archived silently.
- [x] Preserve historical relationships and transactions when parent records are archived.

## Entity lifecycle workflows

- [x] Students: show archived records, archive safely, and restore.
- [x] Course definitions: show inactive records, archive safely, and restore.
- [x] Budget pools: show inactive pools, archive safely, and restore.
- [x] Certificate credit pools: show inactive pools, archive safely, and restore.
- [x] Documents: replace physical deletion with soft archive, protect linked evidence, show archived records, and restore.
- [x] Allocations: add a confirmed cancellation action and prevent cancellation after certificate ordering.
- [x] Deliveries: add a confirmed cancellation action and prevent cancellation while non-terminal allocations remain.

## Reporting and exports

- [x] Add report date-range filtering and an include-archived option where the report data supports it.
- [x] Make general CSV exports explicit about whether archived/inactive data is included.

## Audit and diagnostics

- [x] Audit archive, restore, cancellation, and blocked lifecycle attempts with entity identifiers and reasons.
- [x] Log lifecycle failures and unexpected UI errors to daily rolling `.log` files.
- [x] Show actionable error messages with the log-directory location.
- [x] Verify log retention and shutdown flushing.

## Verification and delivery

- [x] Add service tests for dependency guards, archive/restore, and cancellation rules.
- [x] Build the application and run unit and FlaUI tests.
- [x] Review the final diff, commit, synchronize with origin, and push.
