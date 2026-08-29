# Installer

Run `publish.ps1` from PowerShell 7 to create a self-contained Windows x64 build:

```powershell
installer\publish.ps1
```

Output:

- `release\StudentTracker-win-x64\` – application folder
- `release\StudentTracker-win-x64.zip` – portable archive

Distribute the zip and instruct users to extract it and run `StudentTracker.exe`.
