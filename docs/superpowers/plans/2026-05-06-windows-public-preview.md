# CodexBar for Windows Public Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prepare CodexBar for Windows for a public preview release with Codex, Claude, Cursor, and Gemini support.

**Architecture:** Keep the Windows port inside the existing `src/windows` .NET/WPF structure. Add Cursor and Gemini as normal `IUsageProvider` implementations that emit shared `UsageSnapshot` records, extend settings/paths/UI to expose the new providers, then harden docs, CI, packaging, and attribution for a separate Windows-focused GitHub repository.

**Tech Stack:** .NET 9, WPF, MSTest, PowerShell packaging scripts, GitHub Actions Windows runners.

---

## File Map

- Modify `src/windows/CodexBar.Core/Models/UsageProvider.cs`: add `Cursor` and `Gemini`.
- Modify `src/windows/CodexBar.Core/Settings/AppSettings.cs`: add enabled flags and source/manual cookie fields.
- Modify `src/windows/CodexBar.Core/Settings/JsonSettingsStore.cs`: load/save new settings with safe defaults.
- Modify `src/windows/CodexBar.Core/Paths/IAppPaths.cs`: expose Cursor and Gemini credential/config paths.
- Modify `src/windows/CodexBar.Core/Paths/WindowsAppPaths.cs`: implement Windows paths.
- Create `src/windows/CodexBar.Core/Providers/Cursor/CursorProvider.cs`: Cursor manual-cookie provider.
- Create `src/windows/CodexBar.Core/Providers/Cursor/CursorUsageMapper.cs`: tolerant Cursor response mapping.
- Create `src/windows/CodexBar.Core/Providers/Gemini/GeminiProvider.cs`: Gemini CLI OAuth quota provider.
- Create `src/windows/CodexBar.Core/Providers/Gemini/GeminiCredentials.cs`: Gemini OAuth credential read/write helpers.
- Create `src/windows/CodexBar.Core/Providers/Gemini/GeminiUsageMapper.cs`: quota/tier response mapping.
- Modify `src/windows/CodexBar.WinApp/AppServices.cs`: construct Cursor/Gemini providers when enabled.
- Modify `src/windows/CodexBar.WinApp/ProviderLinks.cs`: add dashboard/status links for Cursor/Gemini.
- Modify `src/windows/CodexBar.WinApp/ViewModels/SettingsViewModel.cs`: bind provider toggles/status/paths.
- Modify `src/windows/CodexBar.WinApp/ViewModels/PopoverViewModel.cs`: colors/icons for Cursor/Gemini.
- Modify `src/windows/CodexBar.WinApp/Views/SettingsWindow.xaml`: expose provider toggles and Cursor manual cookie field.
- Modify `src/windows/CodexBar.WinApp/Views/AboutWindow.xaml`: add upstream attribution.
- Modify `README.md`: make Windows preview first.
- Create `docs/windows-codex.md`, `docs/windows-claude.md`, `docs/windows-cursor.md`, `docs/windows-gemini.md`: Windows-specific provider docs.
- Create `.github/workflows/windows.yml`: Windows build/test/package CI.
- Modify `Scripts/package-windows.ps1`: emit checksum file for public releases.
- Test files: add/modify provider, settings, app service, popover, packaging, and docs tests under `src/windows/CodexBar.Tests`.

---

### Task 1: Extend Core Provider Model And Settings

**Files:**
- Modify: `src/windows/CodexBar.Core/Models/UsageProvider.cs`
- Modify: `src/windows/CodexBar.Core/Settings/AppSettings.cs`
- Modify: `src/windows/CodexBar.Core/Settings/JsonSettingsStore.cs`
- Modify: `src/windows/CodexBar.Core/Paths/IAppPaths.cs`
- Modify: `src/windows/CodexBar.Core/Paths/WindowsAppPaths.cs`
- Test: `src/windows/CodexBar.Tests\JsonSettingsStoreTests.cs`
- Test: `src/windows/CodexBar.Tests\SettingsWindowTests.cs`

- [ ] **Step 1: Write failing settings tests**

Add this test to `JsonSettingsStoreTests`:

```csharp
[TestMethod]
public async Task DefaultsEnablePreviewProvidersAndKeepManualCursorCookieEmpty()
{
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");
    var store = new JsonSettingsStore(path);

    var settings = await store.LoadAsync(CancellationToken.None);

    Assert.IsTrue(settings.CodexEnabled);
    Assert.IsTrue(settings.ClaudeEnabled);
    Assert.IsTrue(settings.CursorEnabled);
    Assert.IsTrue(settings.GeminiEnabled);
    Assert.AreEqual("auto", settings.CursorSource);
    Assert.AreEqual("auto", settings.GeminiSource);
    Assert.IsNull(settings.CursorManualCookieHeader);
}
```

Add this test to `SettingsWindowTests`:

```csharp
[TestMethod]
public void SettingsViewModelReportsCursorAndGeminiCredentialStatus()
{
    var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    try
    {
        var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiOAuthCredentialsJson)!);
        File.WriteAllText(paths.GeminiOAuthCredentialsJson, "{}");

        var settings = AppSettings.Default with { CursorManualCookieHeader = "WorkosCursorSessionToken=abc" };
        var viewModel = new SettingsViewModel(settings, paths);

        Assert.IsTrue(viewModel.CursorEnabled);
        Assert.IsTrue(viewModel.GeminiEnabled);
        Assert.AreEqual("Connected", viewModel.CursorAccountStatus);
        Assert.AreEqual("Connected", viewModel.GeminiAccountStatus);
        Assert.AreEqual(paths.GeminiOAuthCredentialsJson, viewModel.GeminiCredentialPath);
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run the failing tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "JsonSettingsStoreTests.DefaultsEnablePreviewProvidersAndKeepManualCursorCookieEmpty|SettingsWindowTests.SettingsViewModelReportsCursorAndGeminiCredentialStatus" --verbosity minimal
```

Expected: compile failure because `CursorEnabled`, `GeminiEnabled`, provider source fields, and Gemini path members do not exist.

- [ ] **Step 3: Implement core settings and path fields**

Update `UsageProvider.cs`:

```csharp
public enum UsageProvider
{
    Codex,
    Claude,
    Cursor,
    Gemini
}
```

Update `AppSettings` constructor/default:

```csharp
public sealed record AppSettings(
    bool CodexEnabled,
    bool ClaudeEnabled,
    bool CursorEnabled,
    bool GeminiEnabled,
    bool MergeTrayIcon,
    bool ShowUsageAsUsed,
    bool DockOverviewNearTaskbar,
    bool LaunchAtStartup,
    int RefreshMinutes,
    string CodexSource,
    string ClaudeSource,
    string CursorSource,
    string GeminiSource,
    string? ClaudeManualCookieHeader,
    string? CursorManualCookieHeader)
{
    public static AppSettings Default { get; } = new(
        CodexEnabled: true,
        ClaudeEnabled: true,
        CursorEnabled: true,
        GeminiEnabled: true,
        MergeTrayIcon: true,
        ShowUsageAsUsed: true,
        DockOverviewNearTaskbar: false,
        LaunchAtStartup: false,
        RefreshMinutes: 5,
        CodexSource: "auto",
        ClaudeSource: "auto",
        CursorSource: "auto",
        GeminiSource: "auto",
        ClaudeManualCookieHeader: null,
        CursorManualCookieHeader: null);
}
```

Update `StoredAppSettings` and `ToAppSettings()` with nullable fields for `CursorEnabled`, `GeminiEnabled`, `CursorSource`, `GeminiSource`, and `CursorManualCookieHeader`, preserving defaults for missing old config files.

Update `IAppPaths`:

```csharp
string GeminiSettingsJson { get; }
string GeminiOAuthCredentialsJson { get; }
```

Update `WindowsAppPaths`:

```csharp
public string GeminiSettingsJson => Path.Combine(homeDirectory, ".gemini", "settings.json");
public string GeminiOAuthCredentialsJson => Path.Combine(homeDirectory, ".gemini", "oauth_creds.json");
```

Update `SettingsViewModel` to copy the new enabled/source/cookie fields, compute `CursorAccountStatus` from the manual cookie header, and compute `GeminiAccountStatus` from `paths.GeminiOAuthCredentialsJson`.

- [ ] **Step 4: Run the targeted tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "JsonSettingsStoreTests|SettingsWindowTests" --verbosity minimal
```

Expected: all selected tests pass.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/windows/CodexBar.Core/Models/UsageProvider.cs src/windows/CodexBar.Core/Settings/AppSettings.cs src/windows/CodexBar.Core/Settings/JsonSettingsStore.cs src/windows/CodexBar.Core/Paths/IAppPaths.cs src/windows/CodexBar.Core/Paths/WindowsAppPaths.cs src/windows/CodexBar.WinApp/ViewModels/SettingsViewModel.cs src/windows/CodexBar.Tests/JsonSettingsStoreTests.cs src/windows/CodexBar.Tests/SettingsWindowTests.cs
git commit -m "Add Windows preview provider settings"
```

---

### Task 2: Add Cursor Manual Cookie Provider

**Files:**
- Create: `src/windows/CodexBar.Core/Providers/Cursor/CursorProvider.cs`
- Create: `src/windows/CodexBar.Core/Providers/Cursor/CursorUsageMapper.cs`
- Test: `src/windows/CodexBar.Tests\CursorProviderTests.cs`
- Test: `src/windows/CodexBar.Tests\CursorUsageMapperTests.cs`

- [ ] **Step 1: Write failing Cursor mapper tests**

Create `CursorUsageMapperTests.cs`:

```csharp
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Cursor;
using System.Text.Json;

namespace CodexBar.Tests;

[TestClass]
public sealed class CursorUsageMapperTests
{
    [TestMethod]
    public void MapsUsageSummaryIntoPlanAndOnDemandWindows()
    {
        using var usage = JsonDocument.Parse("""
        {
          "includedUsage": 75,
          "includedUsageLimit": 500,
          "onDemandUsage": 12.5,
          "onDemandUsageLimit": 50,
          "billingCycleEnd": "2026-05-30T00:00:00Z"
        }
        """);
        using var account = JsonDocument.Parse("""{ "email": "cursor@example.com", "name": "Cursor User" }""");

        var snapshot = CursorUsageMapper.Map(usage.RootElement, account.RootElement, DateTimeOffset.FromUnixTimeSeconds(1893440000));

        Assert.AreEqual(UsageProvider.Cursor, snapshot.Provider);
        Assert.AreEqual("Cursor", snapshot.DisplayName);
        Assert.AreEqual("cursor@example.com", snapshot.AccountEmail);
        Assert.AreEqual(2, snapshot.Windows.Count);
        Assert.AreEqual("Included plan", snapshot.Windows[0].Title);
        Assert.AreEqual(15, snapshot.Windows[0].UsedPercent);
        Assert.AreEqual("On-demand", snapshot.Windows[1].Title);
        Assert.AreEqual(25, snapshot.Windows[1].UsedPercent);
        Assert.AreEqual(DateTimeOffset.Parse("2026-05-30T00:00:00Z"), snapshot.Windows[0].ResetsAt);
    }

    [TestMethod]
    public void UnknownUsageShapeReturnsNoUsageDataSnapshot()
    {
        using var usage = JsonDocument.Parse("""{ "unexpected": true }""");

        var snapshot = CursorUsageMapper.Map(usage.RootElement, null, DateTimeOffset.FromUnixTimeSeconds(1893440000));

        Assert.AreEqual(UsageProvider.Cursor, snapshot.Provider);
        Assert.AreEqual(0, snapshot.Windows.Count);
        Assert.AreEqual("No usage data", snapshot.ErrorMessage);
        Assert.IsTrue(snapshot.IsStale);
    }
}
```

- [ ] **Step 2: Write failing Cursor provider tests**

Create `CursorProviderTests.cs`:

```csharp
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Cursor;

namespace CodexBar.Tests;

[TestClass]
public sealed class CursorProviderTests
{
    [TestMethod]
    public async Task MissingCookieReturnsMissingCredentialsSnapshot()
    {
        var provider = new CursorProvider(new HttpClient(new QueueHandler()), null);

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(UsageProvider.Cursor, snapshot.Provider);
        Assert.IsTrue(snapshot.IsStale);
        StringAssert.Contains(snapshot.ErrorMessage!, "Cursor cookie");
    }

    [TestMethod]
    public async Task SendsCookieHeaderToCursorApis()
    {
        var handler = new QueueHandler(
            (new Uri("https://cursor.com/api/usage-summary"), """{ "includedUsage": 10, "includedUsageLimit": 100 }"""),
            (new Uri("https://cursor.com/api/auth/me"), """{ "email": "cursor@example.com" }"""));
        var provider = new CursorProvider(new HttpClient(handler), "WorkosCursorSessionToken=abc");

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual("cursor@example.com", snapshot.AccountEmail);
        Assert.IsTrue(handler.Requests.All(request => request.Headers.GetValues("Cookie").Single() == "WorkosCursorSessionToken=abc"));
    }

    [TestMethod]
    public async Task UnauthorizedResponseReturnsRefreshCookieSnapshot()
    {
        var handler = new QueueHandler(HttpStatusCode.Unauthorized, """{}""");
        var provider = new CursorProvider(new HttpClient(handler), "expired=true");

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.IsTrue(snapshot.IsStale);
        StringAssert.Contains(snapshot.ErrorMessage!, "refresh your Cursor cookie");
    }
}
```

Create a private `QueueHandler` inside `CursorProviderTests` so the Cursor provider tests are self-contained and do not depend on helpers from another test file.

- [ ] **Step 3: Run the failing Cursor tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "Cursor" --verbosity minimal
```

Expected: compile failure because Cursor provider files do not exist.

- [ ] **Step 4: Implement Cursor provider and mapper**

Create `CursorProvider`:

```csharp
public sealed class CursorProvider : IUsageProvider
{
    private static readonly Uri UsageSummaryUri = new("https://cursor.com/api/usage-summary");
    private static readonly Uri AccountUri = new("https://cursor.com/api/auth/me");
    private readonly HttpClient httpClient;
    private readonly string? manualCookieHeader;

    public CursorProvider(HttpClient httpClient, string? manualCookieHeader)
    {
        this.httpClient = httpClient;
        this.manualCookieHeader = manualCookieHeader;
    }

    public UsageProvider Provider => UsageProvider.Cursor;

    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(manualCookieHeader))
        {
            return UsageSnapshot.MissingCredentials(UsageProvider.Cursor, "Cursor", "Cursor cookie header was not found. Add it in Settings.");
        }

        using var usage = await GetJsonAsync(UsageSummaryUri, cancellationToken);
        if (usage is null)
        {
            return UsageSnapshot.MissingCredentials(UsageProvider.Cursor, "Cursor", "Cursor rejected the saved cookie. Refresh your Cursor cookie in Settings.");
        }

        using var account = await GetJsonAsync(AccountUri, cancellationToken);
        return CursorUsageMapper.Map(usage.RootElement, account?.RootElement, DateTimeOffset.Now);
    }

    private async Task<JsonDocument?> GetJsonAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Cookie", manualCookieHeader);
        request.Headers.TryAddWithoutValidation("User-Agent", "CodexBar-Windows");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
```

Create `CursorUsageMapper` using `JsonElement` helper methods:

```csharp
public static UsageSnapshot Map(JsonElement usage, JsonElement? account, DateTimeOffset updatedAt)
{
    var windows = new List<RateWindow>();
    var reset = ReadDateTime(usage, "billingCycleEnd") ?? ReadDateTime(usage, "currentPeriodEnd");
    AddPercentWindow(windows, usage, "includedUsage", "includedUsageLimit", "included_plan", "Included plan", reset);
    AddPercentWindow(windows, usage, "onDemandUsage", "onDemandUsageLimit", "on_demand", "On-demand", reset);

    return new UsageSnapshot(
        UsageProvider.Cursor,
        "Cursor",
        updatedAt,
        windows,
        ReadString(account, "email"),
        null,
        null,
        ReadDecimal(usage, "onDemandUsage"),
        null,
        null,
        null,
        "manual cookie",
        windows.Count == 0 ? "No usage data" : null,
        windows.Count == 0);
}
```

Ensure `AddPercentWindow` clamps `used / limit * 100` to `0..100` and skips windows with missing or zero limits.

- [ ] **Step 5: Run Cursor tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "Cursor" --verbosity minimal
```

Expected: Cursor tests pass.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src/windows/CodexBar.Core/Providers/Cursor src/windows/CodexBar.Tests/CursorProviderTests.cs src/windows/CodexBar.Tests/CursorUsageMapperTests.cs
git commit -m "Add Windows Cursor usage provider"
```

---

### Task 3: Add Gemini CLI OAuth Provider

**Files:**
- Create: `src/windows/CodexBar.Core/Providers/Gemini/GeminiCredentials.cs`
- Create: `src/windows/CodexBar.Core/Providers/Gemini/GeminiProvider.cs`
- Create: `src/windows/CodexBar.Core/Providers/Gemini/GeminiUsageMapper.cs`
- Test: `src/windows/CodexBar.Tests\GeminiCredentialsTests.cs`
- Test: `src/windows/CodexBar.Tests\GeminiUsageMapperTests.cs`
- Test: `src/windows/CodexBar.Tests\GeminiProviderTests.cs`

- [ ] **Step 1: Write failing Gemini credential tests**

Create `GeminiCredentialsTests.cs`:

```csharp
using CodexBar.Core.Providers.Gemini;

namespace CodexBar.Tests;

[TestClass]
public sealed class GeminiCredentialsTests
{
    [TestMethod]
    public async Task ReadsGeminiCliOauthCredentials()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "oauth_creds.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
        {
          "access_token": "access",
          "refresh_token": "refresh",
          "id_token": "header.eyJlbWFpbCI6ImdlbWluaUBleGFtcGxlLmNvbSJ9.signature",
          "expiry_date": 1893440000000
        }
        """);

        var credentials = await GeminiCredentials.ReadAsync(path, CancellationToken.None);

        Assert.AreEqual("access", credentials.AccessToken);
        Assert.AreEqual("refresh", credentials.RefreshToken);
        Assert.AreEqual("gemini@example.com", credentials.Email);
        Assert.IsFalse(credentials.IsExpired(DateTimeOffset.FromUnixTimeSeconds(1893430000)));
    }
}
```

- [ ] **Step 2: Write failing Gemini mapper tests**

Create `GeminiUsageMapperTests.cs`:

```csharp
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Gemini;
using System.Text.Json;

namespace CodexBar.Tests;

[TestClass]
public sealed class GeminiUsageMapperTests
{
    [TestMethod]
    public void MapsQuotaBucketsToProAndFlashWindows()
    {
        using var load = JsonDocument.Parse("""{ "tier": { "id": "standard-tier" } }""");
        using var quota = JsonDocument.Parse("""
        {
          "quota": [
            { "modelId": "gemini-2.5-pro", "remainingFraction": 0.25, "resetTime": "2026-05-07T12:00:00Z" },
            { "modelId": "gemini-2.5-flash", "remainingFraction": 0.80, "resetTime": "2026-05-07T13:00:00Z" }
          ]
        }
        """);

        var snapshot = GeminiUsageMapper.Map(load.RootElement, quota.RootElement, "gemini@example.com", DateTimeOffset.FromUnixTimeSeconds(1893440000));

        Assert.AreEqual(UsageProvider.Gemini, snapshot.Provider);
        Assert.AreEqual("Paid", snapshot.Plan);
        Assert.AreEqual("gemini@example.com", snapshot.AccountEmail);
        Assert.AreEqual("Pro models", snapshot.Windows[0].Title);
        Assert.AreEqual(75, snapshot.Windows[0].UsedPercent);
        Assert.AreEqual("Flash models", snapshot.Windows[1].Title);
        Assert.AreEqual(20, snapshot.Windows[1].UsedPercent);
    }
}
```

- [ ] **Step 3: Write failing Gemini provider tests**

Create `GeminiProviderTests.cs`:

```csharp
using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Providers.Gemini;

namespace CodexBar.Tests;

[TestClass]
public sealed class GeminiProviderTests
{
    [TestMethod]
    public async Task MissingCredentialsReturnsMissingCredentialsSnapshot()
    {
        var paths = WindowsAppPaths.ForTest(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), Path.GetTempPath());
        var provider = new GeminiProvider(new HttpClient(new QueueHandler()), paths, new StaticGeminiOAuthClient("id", "secret"));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(UsageProvider.Gemini, snapshot.Provider);
        StringAssert.Contains(snapshot.ErrorMessage!, "Gemini CLI");
    }

    [TestMethod]
    public async Task UnsupportedApiKeyModeReturnsUnsupportedSourceSnapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiSettingsJson)!);
        await File.WriteAllTextAsync(paths.GeminiSettingsJson, """{ "selectedAuthType": "api-key" }""");
        var provider = new GeminiProvider(new HttpClient(new QueueHandler()), paths, new StaticGeminiOAuthClient("id", "secret"));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        StringAssert.Contains(snapshot.ErrorMessage!, "Gemini CLI OAuth");
    }
}
```

Use a private `QueueHandler` and a tiny test implementation for OAuth client lookup if the implementation defines an interface.

- [ ] **Step 4: Run failing Gemini tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "Gemini" --verbosity minimal
```

Expected: compile failure because Gemini provider files do not exist.

- [ ] **Step 5: Implement Gemini credentials**

Create `GeminiCredentials` as a sealed record:

```csharp
public sealed record GeminiCredentials(
    string? AccessToken,
    string? RefreshToken,
    string? IdToken,
    DateTimeOffset? ExpiresAt)
{
    public string? Email => JwtEmail(IdToken);
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is not null && ExpiresAt <= now.AddMinutes(1);
}
```

Implement `ReadAsync` with `JsonDocument`, supporting `access_token`, `refresh_token`, `id_token`, and millisecond `expiry_date`. Implement JWT payload decoding with Base64Url padding and `JsonDocument`, returning the `email` claim when present. Do not log token values.

- [ ] **Step 6: Implement Gemini mapper**

Create `GeminiUsageMapper.Map(JsonElement loadCodeAssist, JsonElement quota, string? email, DateTimeOffset updatedAt)`.

Rules:

- Plan is `Paid` for `standard-tier`.
- Plan is `Workspace` for `free-tier` with an `hd` claim anywhere in the load response.
- Plan is `Free` for plain `free-tier`.
- Plan is `Legacy` for `legacy-tier`.
- Read quota buckets from `quota`, `quotas`, `quotaBuckets`, or `usage`.
- A bucket is Pro-family when `modelId` contains `pro`.
- A bucket is Flash-family when `modelId` contains `flash`.
- `remainingFraction` maps to used percent as `(1 - remainingFraction) * 100`.
- Lowest remaining fraction wins per family.

- [ ] **Step 7: Implement Gemini provider**

Create `GeminiProvider`:

- Return missing credentials if `paths.GeminiOAuthCredentialsJson` does not exist.
- If `paths.GeminiSettingsJson` exists and selected auth type is `api-key` or `vertex-ai`, return a stale unsupported-source snapshot.
- Read credentials.
- If access token is missing, return missing credentials.
- If expired and refresh token exists, call `https://oauth2.googleapis.com/token` with client ID/secret and persist refreshed credentials.
- Call `loadCodeAssist` and `retrieveUserQuota` using `Authorization: Bearer <token>`.
- Return `GeminiUsageMapper.Map(...)`.

Keep OAuth client lookup behind an injectable interface:

```csharp
public interface IGeminiOAuthClientProvider
{
    Task<(string ClientId, string ClientSecret)?> ReadClientAsync(CancellationToken cancellationToken);
}
```

The first implementation should search common Windows npm install roots under `%APPDATA%\npm\node_modules\@google\gemini-cli` and `%USERPROFILE%\AppData\Roaming\npm\node_modules\@google\gemini-cli`, reading `oauth2.js` for `OAUTH_CLIENT_ID` and `OAUTH_CLIENT_SECRET`.

- [ ] **Step 8: Run Gemini tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "Gemini" --verbosity minimal
```

Expected: Gemini tests pass.

- [ ] **Step 9: Commit**

Run:

```powershell
git add src/windows/CodexBar.Core/Providers/Gemini src/windows/CodexBar.Tests/GeminiCredentialsTests.cs src/windows/CodexBar.Tests/GeminiUsageMapperTests.cs src/windows/CodexBar.Tests/GeminiProviderTests.cs
git commit -m "Add Windows Gemini usage provider"
```

---

### Task 4: Wire Providers Into App Services, Settings, And Popover

**Files:**
- Modify: `src/windows/CodexBar.WinApp/AppServices.cs`
- Modify: `src/windows/CodexBar.WinApp/ProviderLinks.cs`
- Modify: `src/windows/CodexBar.WinApp/ViewModels/SettingsViewModel.cs`
- Modify: `src/windows/CodexBar.WinApp/ViewModels/PopoverViewModel.cs`
- Modify: `src/windows/CodexBar.WinApp/Views/SettingsWindow.xaml`
- Test: `src/windows/CodexBar.Tests\AppServicesTests.cs`
- Test: `src/windows/CodexBar.Tests\PopoverViewModelTests.cs`
- Test: `src/windows/CodexBar.Tests\WpfShellTests.cs`

- [ ] **Step 1: Write failing integration tests**

Update `AppServicesTests.CreatesCodexAndClaudeProvidersAndStoresMissingCredentialSnapshots` to expect four providers:

```csharp
CollectionAssert.AreEqual(
    new[] { UsageProvider.Codex, UsageProvider.Claude, UsageProvider.Cursor, UsageProvider.Gemini },
    services.Providers.Select(provider => provider.Provider).ToArray());
```

Add an omission test:

```csharp
[TestMethod]
public void OmitsDisabledPreviewProviders()
{
    var paths = WindowsAppPaths.ForTest(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), Path.GetTempPath());
    using var services = new AppServices(paths, AppSettings.Default with { CursorEnabled = false, GeminiEnabled = false });

    CollectionAssert.AreEqual(
        new[] { UsageProvider.Codex, UsageProvider.Claude },
        services.Providers.Select(provider => provider.Provider).ToArray());
}
```

Add a popover test with four snapshots asserting tab titles `Codex`, `Claude`, `Cursor`, `Gemini`.

- [ ] **Step 2: Run failing integration tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "AppServicesTests|PopoverViewModelTests" --verbosity minimal
```

Expected: failures until providers are constructed and popover has icon/color handling.

- [ ] **Step 3: Wire AppServices**

Update usings:

```csharp
using CodexBar.Core.Providers.Cursor;
using CodexBar.Core.Providers.Gemini;
```

Add provider construction:

```csharp
if (settings.CursorEnabled)
{
    providers.Add(new CursorProvider(HttpClient, settings.CursorManualCookieHeader));
}

if (settings.GeminiEnabled)
{
    providers.Add(new GeminiProvider(HttpClient, Paths));
}
```

- [ ] **Step 4: Wire ProviderLinks**

Use these initial links:

```csharp
UsageProvider.Cursor => new Uri("https://cursor.com/settings"),
UsageProvider.Gemini => new Uri("https://aistudio.google.com/usage"),
```

Status links:

```csharp
UsageProvider.Cursor => new Uri("https://status.cursor.com/"),
UsageProvider.Gemini => new Uri("https://status.cloud.google.com/"),
```

- [ ] **Step 5: Update Settings UI**

Add toggles below Claude:

```xml
<CheckBox Content="Enable Cursor" IsChecked="{Binding CursorEnabled}" Margin="0,6" />
<CheckBox Content="Enable Gemini" IsChecked="{Binding GeminiEnabled}" Margin="0,6" />
```

Add account rows for Cursor and Gemini. Cursor path can display `Manual cookie header`; Gemini path binds to `GeminiCredentialPath`.

Add Cursor manual cookie field below Claude:

```xml
<TextBlock Text="Cursor manual cookie header" Margin="0,12,0,4" />
<TextBox Text="{Binding CursorManualCookieHeader, UpdateSourceTrigger=PropertyChanged}"
         Height="80"
         TextWrapping="Wrap"
         AcceptsReturn="True" />
```

If the settings content no longer fits comfortably, wrap the middle StackPanel in a `ScrollViewer` while keeping Save/Cancel fixed.

- [ ] **Step 6: Update Popover colors/icons**

Change `ProgressColor` to a switch:

```csharp
private static string ProgressColor(UsageProvider provider) =>
    provider switch
    {
        UsageProvider.Claude => "#C87950",
        UsageProvider.Cursor => "#2F7BF6",
        UsageProvider.Gemini => "#5FAD56",
        _ => "#56B3A7"
    };
```

Change `ProviderIconGeometry` to a switch. For this preview task, return `CodexIconGeometry` for Cursor and Gemini so every tab renders a stable icon while provider-specific vector icons are handled as a visual polish task after public-preview functionality is in place.

- [ ] **Step 7: Run integration tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "AppServicesTests|PopoverViewModelTests|WpfShellTests|SettingsWindowTests" --verbosity minimal
```

Expected: selected tests pass.

- [ ] **Step 8: Commit**

Run:

```powershell
git add src/windows/CodexBar.WinApp/AppServices.cs src/windows/CodexBar.WinApp/ProviderLinks.cs src/windows/CodexBar.WinApp/ViewModels/SettingsViewModel.cs src/windows/CodexBar.WinApp/ViewModels/PopoverViewModel.cs src/windows/CodexBar.WinApp/Views/SettingsWindow.xaml src/windows/CodexBar.Tests/AppServicesTests.cs src/windows/CodexBar.Tests/PopoverViewModelTests.cs src/windows/CodexBar.Tests/WpfShellTests.cs src/windows/CodexBar.Tests/SettingsWindowTests.cs
git commit -m "Wire Windows preview providers into UI"
```

---

### Task 5: Public README, Attribution, And Provider Docs

**Files:**
- Modify: `README.md`
- Modify: `src/windows/CodexBar.WinApp/Views/AboutWindow.xaml`
- Create: `docs/windows-codex.md`
- Create: `docs/windows-claude.md`
- Create: `docs/windows-cursor.md`
- Create: `docs/windows-gemini.md`
- Test: `src/windows/CodexBar.Tests\PublicReleaseDocsTests.cs`

- [ ] **Step 1: Write failing docs tests**

Create `PublicReleaseDocsTests.cs`:

```csharp
namespace CodexBar.Tests;

[TestClass]
public sealed class PublicReleaseDocsTests
{
    [TestMethod]
    public void ReadmePositionsWindowsPreviewAndCreditsOriginalProject()
    {
        var readme = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "README.md")));

        StringAssert.Contains(readme, "CodexBar for Windows");
        StringAssert.Contains(readme, "https://github.com/steipete/CodexBar");
        StringAssert.Contains(readme, "Windows 11");
        StringAssert.Contains(readme, "Cursor");
        StringAssert.Contains(readme, "Gemini");
        StringAssert.Contains(readme, "credentials stay on your machine");
    }

    [TestMethod]
    public void AboutWindowCreditsOriginalProject()
    {
        var about = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CodexBar.WinApp", "Views", "AboutWindow.xaml")));

        StringAssert.Contains(about, "Inspired by Peter Steinberger's CodexBar");
    }
}
```

- [ ] **Step 2: Run failing docs tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "PublicReleaseDocsTests" --verbosity minimal
```

Expected: tests fail until README/About docs are updated.

- [ ] **Step 3: Rewrite README for Windows preview**

Keep the README concise and Windows-first:

```markdown
# CodexBar for Windows

CodexBar for Windows is a Windows 11 tray app that keeps AI coding-provider usage visible without opening every provider dashboard.

This project is inspired by Peter Steinberger's original CodexBar for macOS: https://github.com/steipete/CodexBar. The Windows port is maintained separately.
```

Required sections:

- Install
- Supported Providers
- First Run
- Provider Setup
- Privacy
- Known Limitations
- Attribution
- Contributing
- License

State that the first public preview supports Codex, Claude, Cursor, and Gemini.

- [ ] **Step 4: Add Windows provider docs**

Create the four `docs/windows-*.md` files. Each file must include:

- credential source path or setup source,
- what the app reads,
- what endpoint family it calls,
- what is displayed,
- common setup errors.

Cursor doc must say manual cookie header is preview-only. Gemini doc must say Gemini CLI OAuth is required for preview support.

- [ ] **Step 5: Update About window attribution**

Increase About window height if needed and add:

```xml
<TextBlock Text="Inspired by Peter Steinberger's CodexBar"
           Margin="0,8,0,0"
           FontSize="12"
           TextWrapping="Wrap"
           Foreground="{StaticResource CodexBarMutedTextBrush}" />
```

- [ ] **Step 6: Run docs tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "PublicReleaseDocsTests" --verbosity minimal
```

Expected: docs tests pass.

- [ ] **Step 7: Commit**

Run:

```powershell
git add README.md src/windows/CodexBar.WinApp/Views/AboutWindow.xaml docs/windows-codex.md docs/windows-claude.md docs/windows-cursor.md docs/windows-gemini.md src/windows/CodexBar.Tests/PublicReleaseDocsTests.cs
git commit -m "Document Windows public preview"
```

---

### Task 6: Windows CI And Release Packaging

**Files:**
- Create: `.github/workflows/windows.yml`
- Modify: `Scripts/package-windows.ps1`
- Test: `src/windows/CodexBar.Tests\PackagingScriptTests.cs`

- [ ] **Step 1: Write failing packaging checksum test**

Add to `PackagingScriptTests` or create it if absent:

```csharp
[TestMethod]
public void WindowsPackageScriptWritesSha256Checksum()
{
    var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Scripts", "package-windows.ps1"));
    var script = File.ReadAllText(scriptPath);

    StringAssert.Contains(script, "Get-FileHash");
    StringAssert.Contains(script, ".sha256");
}
```

- [ ] **Step 2: Run failing packaging test**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "PackagingScriptTests" --verbosity minimal
```

Expected: fail because checksum output is not implemented.

- [ ] **Step 3: Add checksum output**

Append to `Scripts/package-windows.ps1` after `Compress-Archive`:

```powershell
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath
$checksumPath = "$zipPath.sha256"
"$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $zipPath)" | Set-Content -LiteralPath $checksumPath -Encoding ascii
```

Return `ChecksumPath` in the output object.

- [ ] **Step 4: Add Windows workflow**

Create `.github/workflows/windows.yml`:

```yaml
name: Windows

on:
  push:
    branches: ["*"]
  pull_request:
  workflow_dispatch:

concurrency:
  group: windows-${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  build-test-package:
    runs-on: windows-2025
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v6
      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: 9.0.x
      - name: Restore
        run: dotnet restore src/windows/CodexBar.Windows.sln
      - name: Test
        run: dotnet test src/windows/CodexBar.Windows.sln --configuration Release --verbosity minimal
      - name: Package
        shell: pwsh
        run: ./Scripts/package-windows.ps1 -DotNet dotnet
      - name: Upload portable artifact
        uses: actions/upload-artifact@v5
        with:
          name: CodexBar-Windows-portable
          path: |
            dist/windows/*.zip
            dist/windows/*.zip.sha256
```

- [ ] **Step 5: Run packaging test and script locally**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "PackagingScriptTests" --verbosity minimal
powershell -ExecutionPolicy Bypass -File .\Scripts\package-windows.ps1 -DotNet C:\tmp\dotnet\dotnet.exe
```

Expected: test passes, zip and `.sha256` are created under `dist/windows`.

- [ ] **Step 6: Commit**

Run:

```powershell
git add .github/workflows/windows.yml Scripts/package-windows.ps1 src/windows/CodexBar.Tests/PackagingScriptTests.cs
git commit -m "Add Windows public preview packaging CI"
```

---

### Task 7: Final Public Preview Verification

**Files:**
- Modify only files needed to fix verification failures.

- [ ] **Step 1: Run full Windows test suite**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --verbosity minimal
```

Expected: all tests pass.

- [ ] **Step 2: Refresh public artifacts**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\package-windows.ps1 -DotNet C:\tmp\dotnet\dotnet.exe
```

Expected: portable zip and `.sha256` exist under `dist/windows`.

- [ ] **Step 3: Refresh local portable test install**

Run:

```powershell
$out = 'C:\tmp\CodexBar-Windows-Portable'
Get-Process -Name CodexBar.WinApp -ErrorAction SilentlyContinue | Stop-Process -Force
$resolvedParent = Resolve-Path -LiteralPath 'C:\tmp'
$targetFull = [System.IO.Path]::GetFullPath($out)
if (-not $targetFull.StartsWith($resolvedParent.Path, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Refusing to remove unexpected path: $targetFull" }
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
C:\tmp\dotnet\dotnet.exe publish src\windows\CodexBar.WinApp\CodexBar.WinApp.csproj -c Release -r win-x64 --self-contained true -o $out --verbosity minimal
```

Expected: `C:\tmp\CodexBar-Windows-Portable\CodexBar.WinApp.exe` is replaced.

- [ ] **Step 4: Smoke launch local portable app**

Run:

```powershell
$exe = 'C:\tmp\CodexBar-Windows-Portable\CodexBar.WinApp.exe'
$file = Get-Item -LiteralPath $exe
$p = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 8
$alive = -not $p.HasExited
if ($alive) { Stop-Process -Id $p.Id -Force }
[pscustomobject]@{
  Exe = $file.FullName
  LastWriteTime = $file.LastWriteTime
  StartedAndStayedAlive = $alive
} | Format-List
```

Expected: `StartedAndStayedAlive : True`.

- [ ] **Step 5: Confirm final tree status**

Run:

```powershell
git status --short
```

Expected: no uncommitted source changes. If there are uncommitted source changes from fixing verification failures, return to the task that owns those files, run its tests again, and commit using that task's commit instruction.

- [ ] **Step 6: Public repo handoff checklist**

Before creating the public GitHub release, confirm:

- `origin` points to the separate Windows-focused repo.
- `upstream` points to `https://github.com/steipete/CodexBar`.
- README includes Windows preview support and attribution.
- `LICENSE` still includes the original MIT license text.
- GitHub Actions Windows workflow passes on the public repo.
- Release is marked prerelease.
