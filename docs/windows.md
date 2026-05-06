# Windows development

The Windows CodexBar port lives under `src/windows`.

## Requirements

- Windows 11
- .NET 9 SDK
- Existing Codex and/or Claude credentials:
  - Codex: `%CODEX_HOME%\auth.json` or `%USERPROFILE%\.codex\auth.json`
  - Claude: `%USERPROFILE%\.claude\.credentials.json` or manual cookie header in settings

## Build

```powershell
dotnet build src\windows\CodexBar.Windows.sln
```

## Test

```powershell
dotnet test src\windows\CodexBar.Windows.sln
```

## Run

```powershell
dotnet run --project src\windows\CodexBar.WinApp\CodexBar.WinApp.csproj
```

The app starts in the notification area. Click the CodexBar tray icon to open the popover.
