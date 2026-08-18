# Backup and Restore

## Manual Backup

Use the Backup command in the application. This creates a zip containing:

- `Database/student-tracker.db`
- `Documents/`
- `Templates/`

## Restore

1. Open the Restore command in the application.
2. Select a backup zip.
3. The application creates a pre-restore backup automatically.
4. The selected backup is extracted and the database integrity check runs.

## Automatic Backups

Daily backups are created on startup and old backups are cleaned up according to retention policy.
