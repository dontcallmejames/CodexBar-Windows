## Summary

- 

## Windows tests

- [ ] `dotnet test src/windows/CodexBar.Windows.sln --configuration Release --verbosity minimal`
- [ ] `powershell -ExecutionPolicy Bypass -File .\Scripts\package-windows.ps1 -DotNet dotnet` when packaging or release files changed

## Notes

- Provider credentials, cookies, and OAuth files are never included in tests, logs, screenshots, or fixtures.
