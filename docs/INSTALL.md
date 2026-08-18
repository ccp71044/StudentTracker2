# Installation Instructions

## Self-Contained Build

1. Extract `release\StudentTracker-win-x64.zip` to a folder such as `C:\Program Files\StudentTracker`.
2. Run `StudentTracker.Wpf.exe`.
3. The application creates its data directory under `%LOCALAPPDATA%\StudentTracker\` on first run.

No separate runtime installation is required for the self-contained build.

## Uninstall

Delete the application folder. User data in `%LOCALAPPDATA%\StudentTracker\` is not removed unless you delete it manually.
