# Repository Guidelines

## Project Structure & Modules
- `src/windows`: Windows 11 app source. `CodexBar.WinUI` owns the WinUI 3 shell, tray/popover windows, settings, and shell behavior. `CodexBar.Core` owns provider models, credential discovery, usage mapping, update checks, and shared services.
- `src/windows/CodexBar.Tests`: MSTest coverage for provider parsing, settings persistence, update checks, packaging, and docs. Mirror behavior changes with focused tests.
- `installer/windows`: Inno Setup installer source and installer assets.
- `Scripts`: Windows packaging helpers only: `package-windows.ps1` and `package-windows-installer.ps1`.
- `docs`: Windows-facing setup, release, and support docs.
- `legacy-macos`: Archived upstream Swift/macOS app, Sparkle appcast, Swift tests, and old release scripts. Keep it as reference unless the task is explicitly about legacy macOS.
- `legacy-wpf`: Archived WPF shell (`CodexBar.WinApp`, `CodexBar.Tray`) and WPF-specific tests. Not built by the active solution. Keep as reference only.

## Build, Test, Run
- Full Windows test pass: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --verbosity minimal`.
- Package portable build: `.\Scripts\package-windows.ps1`.
- Package installer build: `.\Scripts\package-windows-installer.ps1`; install Inno Setup first if missing.
- Validate against a freshly built Windows binary before handoff when app behavior changes. Do not use legacy macOS scripts for Windows validation.
- Legacy Swift checks, when explicitly needed: `cd legacy-macos; swift test`. Legacy workflows are manual-only.

## Coding Style & Naming
- Follow the existing C# style: nullable-aware code, small services/view models, provider-specific types, and focused helpers instead of broad abstractions.
- Keep provider data siloed. Never display identity, plan, usage, or credential state from one provider inside another provider's UI.
- Prefer explicit names that match user-visible provider concepts. Keep UI text short and Windows-native.
- Avoid adding dependencies or new tooling without confirmation.

## Testing Guidelines
- Add or update MSTest coverage under `src/windows/CodexBar.Tests/*Tests.cs` for behavior changes.
- Use real parsers, mappers, and settings stores in tests where practical. Mock only external network/process boundaries.
- Run the full Windows test command before handoff after code or workflow changes.
- For UI positioning/visual behavior, prefer tests around view-model state and shell positioning helpers before manual checks.

## Commit & PR Guidelines
- Commit messages: short imperative clauses, such as `Fix provider tab refresh` or `Archive legacy macOS sources`.
- PRs/patches should list summary, commands run, screenshots/GIFs for visible UI changes, and linked issue/reference when relevant.
- Windows releases are produced by `.github/workflows/windows.yml` from tags. Legacy Swift/CLI/upstream workflows must stay manual-only unless the repo intentionally resumes macOS releases.

## Agent Notes
- The public product is the Windows app. Keep root-level docs, scripts, workflows, and release paths Windows-first.
- Root `version.env` is the Windows preview version source. Do not move it into `legacy-macos`.
- `.claude/`, `dist/`, and local generated output may exist in the worktree; do not remove or revert user/local artifacts unless explicitly requested.
- If the app itself changes, rebuild/package with the Windows scripts and make clear which binary was validated.
