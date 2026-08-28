# Installation Instructions

## Self-Contained Build

1. Download `StudentTracker-win-x64-<version>.zip` from the GitHub release for the version you want.
2. Right-click the zip, choose Properties and tick **Unblock** (Windows marks downloaded files), then extract it
   to a folder such as `C:\Program Files\StudentTracker`.
3. Run `StudentTracker.Wpf.exe`.
4. The application creates its data directory under `%LOCALAPPDATA%\StudentTracker\` on first run and starts
   with an empty register.

No separate runtime installation is required for the self-contained build. The build is not code-signed, so
Windows SmartScreen may warn on first run — choose **More info → Run anyway**.

## Demonstration data

To populate a fresh installation with example students, courses and deliveries for training or evaluation,
start the application once with:

```powershell
StudentTracker.Wpf.exe --sample-data
```

Sample data is only inserted when the register is empty. Never use this switch on a live installation.

## Upgrading

Extract the new version over the old folder. Database migrations run automatically on startup; user data under
`%LOCALAPPDATA%\StudentTracker\` is preserved. Take a backup first (see `release/BACKUP_RESTORE.md`).

## Uninstall

Delete the application folder. User data in `%LOCALAPPDATA%\StudentTracker\` is not removed unless you delete it manually.
