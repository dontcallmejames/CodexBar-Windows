# Antigravity Provider Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an Antigravity usage provider to the CodexBar Windows app that reads Claude / Gemini Pro / Gemini Flash quota from the local Antigravity language server and shows it as a provider card, restoring the Gemini visibility lost when Google retired the consumer Gemini CLI on 2026-06-18.

**Architecture:** A new `AntigravityProvider : IUsageProvider` orchestrates three isolated pieces: `IAntigravityProcessLocator` (finds the running language-server PID, its loopback ports, and CSRF token — the only OS-specific code), `AntigravityLanguageServerClient` (POSTs the RPC chain over an injected loopback-trusting `HttpClient`), and `AntigravityUsageMapper` (pure JSON → `UsageSnapshot`). Provider, client, and mapper are unit-tested with a faked locator and mocked HTTP; the Windows locator is verified manually against a running Antigravity.

**Tech Stack:** C# / .NET 9 (`net9.0-windows`), MSTest, WinUI 3, `System.Text.Json`, `System.Management` (WMI, new dependency), `iphlpapi.dll` P/Invoke.

**Spec:** `docs/superpowers/specs/2026-06-25-antigravity-provider-design.md`

**Build / test command (from repo root):**
`C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --verbosity minimal`
Single class: append `--filter "FullyQualifiedName~<TestClassName>"`.

**Protocol reference (ground truth, from steipete/CodexBar `AntigravityStatusProbe.swift`):**
- URL: `{scheme}://127.0.0.1:{port}/exa.language_server_pb.LanguageServerService/{Method}`, schemes tried `https` then `http`.
- Headers: `X-Codeium-Csrf-Token: {token}` (empty for the `agy` CLI), `Connect-Protocol-Version: 1`, `Content-Type: application/json`.
- Methods in priority order: `RetrieveUserQuotaSummary` (body `{"forceRefresh":true}`), `GetUserStatus` (body `{"metadata":{"ideName":"antigravity","extensionName":"antigravity","ideVersion":"unknown","locale":"en"}}`), `GetCommandModelConfigs` (same metadata body).
- `RetrieveUserQuotaSummary` response: `groups[].displayName`, `groups[].buckets[]` = `{bucketId, displayName, remainingFraction, resetTime, resetDescription, disabled}`.
- `GetUserStatus` response: `userStatus.cascadeModelConfigData.clientModelConfigs[]` = `{label, modelOrAlias:{model}, quotaInfo:{remainingFraction, resetTime}}`; plan `userStatus.userTier.preferredName` (fallback `userStatus.planStatus.planInfo.preferredName`); email `accountEmail`.

---

## File Structure

**Create:**
- `src/windows/CodexBar.Core/Providers/Antigravity/AntigravityUsageMapper.cs` — pure JSON → `UsageSnapshot`.
- `src/windows/CodexBar.Core/Providers/Antigravity/IAntigravityProcessLocator.cs` — interface + `AntigravityCandidate` record.
- `src/windows/CodexBar.Core/Providers/Antigravity/AntigravityLanguageServerClient.cs` — RPC chain + `AntigravityQuotaResponse`.
- `src/windows/CodexBar.Core/Providers/Antigravity/AntigravityProvider.cs` — `IUsageProvider` orchestration.
- `src/windows/CodexBar.Core/Providers/Antigravity/WindowsAntigravityProcessLocator.cs` — WMI + iphlpapi + CSRF parsing.
- `src/windows/CodexBar.Tests/AntigravityUsageMapperTests.cs`
- `src/windows/CodexBar.Tests/AntigravityLanguageServerClientTests.cs`
- `src/windows/CodexBar.Tests/AntigravityProviderTests.cs`
- `docs/windows-antigravity.md` — setup page.

**Modify:**
- `src/windows/CodexBar.Core/Models/UsageProvider.cs` — add `Antigravity`.
- `src/windows/CodexBar.Core/Providers/ProviderLinks.cs` — add `Antigravity` cases.
- `src/windows/CodexBar.Core/Settings/AppSettings.cs` — add `AntigravityEnabled` + `AntigravitySource`.
- `src/windows/CodexBar.Core/CodexBar.Core.csproj` — add `System.Management` package.
- `src/windows/CodexBar.WinUI/ViewModels/SettingsViewModel.cs` — add `AntigravityEnabled` property + ctor init + `ToSettings`.
- `src/windows/CodexBar.WinUI/Views/SettingsWindow.xaml` — add Antigravity toggle card.
- `src/windows/CodexBar.WinUI/AppHostBuilder.cs` — loopback `HttpClient`, register provider, `IsEnabled` case.
- `src/windows/CodexBar.Core/Providers/Gemini/GeminiProvider.cs` — deprecation message on auth failure.
- `src/windows/CodexBar.Tests/GeminiProviderTests.cs` — deprecation message test.

---

## Task 1: Add the enum value and provider links

**Files:**
- Modify: `src/windows/CodexBar.Core/Models/UsageProvider.cs`
- Modify: `src/windows/CodexBar.Core/Providers/ProviderLinks.cs`

- [ ] **Step 1: Add the enum value**

In `src/windows/CodexBar.Core/Models/UsageProvider.cs`, add `Antigravity` after `Copilot`:

```csharp
public enum UsageProvider
{
    Codex,
    Claude,
    Cursor,
    Gemini,
    Copilot,
    Antigravity
}
```

- [ ] **Step 2: Add ProviderLinks cases**

In `src/windows/CodexBar.Core/Providers/ProviderLinks.cs`, add an `Antigravity` arm to each of the three switches (place before the `_ =>` default):

`SetupUri`:
```csharp
            UsageProvider.Antigravity => new Uri("https://github.com/dontcallmejames/CodexBar-Windows/blob/main/docs/windows-antigravity.md"),
```
`DashboardUri`:
```csharp
            UsageProvider.Antigravity => new Uri("https://antigravity.google"),
```
`StatusUri`:
```csharp
            UsageProvider.Antigravity => new Uri("https://status.cloud.google.com/"),
```

- [ ] **Step 3: Build to verify it compiles**

Run: `C:\tmp\dotnet\dotnet.exe build src\windows\CodexBar.Windows.sln --configuration Release --verbosity minimal`
Expected: Build succeeded. (A switch over `UsageProvider` elsewhere may warn but the `_ =>` defaults keep it compiling.)

- [ ] **Step 4: Commit**

```bash
git add src/windows/CodexBar.Core/Models/UsageProvider.cs src/windows/CodexBar.Core/Providers/ProviderLinks.cs
git commit -m "Add Antigravity to UsageProvider enum and provider links"
```

---

## Task 2: Add the settings flag, view-model property, and toggle UI

**Files:**
- Modify: `src/windows/CodexBar.Core/Settings/AppSettings.cs`
- Modify: `src/windows/CodexBar.WinUI/ViewModels/SettingsViewModel.cs`
- Modify: `src/windows/CodexBar.WinUI/Views/SettingsWindow.xaml`
- Modify: `src/windows/CodexBar.WinUI/AppHostBuilder.cs` (the `IsEnabled` switch only)
- Test: `src/windows/CodexBar.Tests/SettingsRoundTripTests.cs` (create if absent; otherwise add to the existing settings test class — search `CodexBar.Tests` for `AppSettings` tests first)

- [ ] **Step 1: Write the failing test**

Search for an existing settings test class: `grep -rl "AppSettings.Default" src/windows/CodexBar.Tests`. If one exists, add this method there and reuse its namespace/usings. Otherwise create `src/windows/CodexBar.Tests/SettingsRoundTripTests.cs`:

```csharp
using CodexBar.Core.Settings;
using CodexBar.WinUI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class SettingsRoundTripTests
{
    [TestMethod]
    public void AntigravityEnabled_DefaultsTrue_AndSurvivesViewModelRoundTrip()
    {
        Assert.IsTrue(AppSettings.Default.AntigravityEnabled);

        var vm = new SettingsViewModel(AppSettings.Default) { AntigravityEnabled = false };

        Assert.IsFalse(vm.ToSettings().AntigravityEnabled);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --filter "FullyQualifiedName~AntigravityEnabled_DefaultsTrue" --verbosity minimal`
Expected: FAIL — `AppSettings` has no `AntigravityEnabled` (compile error).

- [ ] **Step 3: Add the settings fields**

In `src/windows/CodexBar.Core/Settings/AppSettings.cs`, add `AntigravityEnabled` to the record parameter list (after `CopilotEnabled`) and `AntigravitySource` (after `CopilotSource`):

```csharp
    bool CopilotEnabled,
    bool AntigravityEnabled,
    bool MergeTrayIcon,
```
```csharp
    string CopilotSource,
    string AntigravitySource,
    string? ClaudeManualCookieHeader,
```

In `AppSettings.Default`, add the matching named arguments:
```csharp
        CopilotEnabled: false,
        AntigravityEnabled: true,
        MergeTrayIcon: true,
```
```csharp
        CopilotSource: "auto",
        AntigravitySource: "auto",
        ClaudeManualCookieHeader: null,
```

- [ ] **Step 4: Add the view-model property and round-trip wiring**

In `src/windows/CodexBar.WinUI/ViewModels/SettingsViewModel.cs`:

Add the observable property after `copilotEnabled` (line ~27):
```csharp
    [ObservableProperty] private bool antigravityEnabled;
```
Add the ctor initializer after `copilotEnabled = settings.CopilotEnabled;` (line ~87):
```csharp
        antigravityEnabled = settings.AntigravityEnabled;
```
In `ToSettings()`, add the two new arguments in the same positions as the record (after `CopilotEnabled,` and after `originalSettings.CopilotSource,`):
```csharp
        CopilotEnabled,
        AntigravityEnabled,
        originalSettings.MergeTrayIcon,
```
```csharp
        originalSettings.CopilotSource,
        originalSettings.AntigravitySource,
        string.IsNullOrWhiteSpace(ClaudeManualCookieHeader) ? null : ClaudeManualCookieHeader,
```

- [ ] **Step 5: Add the toggle to the Settings window**

In `src/windows/CodexBar.WinUI/Views/SettingsWindow.xaml`, add a card after the Copilot card (after line ~39, before the `Refresh` subtitle):
```xml
                <ctk:SettingsCard Header="Antigravity" Description="Reads Claude + Gemini quota from the local Antigravity language server. Antigravity must be running.">
                    <ToggleSwitch AutomationProperties.AutomationId="SettingsAntigravityEnabledToggle"
                                  IsOn="{x:Bind ViewModel.AntigravityEnabled, Mode=TwoWay}" />
                </ctk:SettingsCard>
```

- [ ] **Step 6: Add the IsEnabled case**

In `src/windows/CodexBar.WinUI/AppHostBuilder.cs`, add to the `IsEnabled` switch (after the `Copilot` arm, line ~89):
```csharp
        UsageProvider.Antigravity => s.AntigravityEnabled,
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --filter "FullyQualifiedName~AntigravityEnabled_DefaultsTrue" --verbosity minimal`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/windows/CodexBar.Core/Settings/AppSettings.cs src/windows/CodexBar.WinUI/ViewModels/SettingsViewModel.cs src/windows/CodexBar.WinUI/Views/SettingsWindow.xaml src/windows/CodexBar.WinUI/AppHostBuilder.cs src/windows/CodexBar.Tests/SettingsRoundTripTests.cs
git commit -m "Add Antigravity enabled setting and toggle"
```

---

## Task 3: Add the System.Management dependency

**Files:**
- Modify: `src/windows/CodexBar.Core/CodexBar.Core.csproj`

- [ ] **Step 1: Add the package reference**

In `src/windows/CodexBar.Core/CodexBar.Core.csproj`, add to the existing `<ItemGroup>` (match the repo's 9.0.x line used by the other packages):

```xml
    <PackageReference Include="System.Management" Version="9.0.5" />
```

- [ ] **Step 2: Restore + build to verify it resolves**

Run: `C:\tmp\dotnet\dotnet.exe build src\windows\CodexBar.Core\CodexBar.Core.csproj --configuration Release --verbosity minimal`
Expected: Build succeeded, package restored. If 9.0.5 does not resolve, use the latest `9.0.*` shown by `C:\tmp\dotnet\dotnet.exe list src\windows\CodexBar.Core\CodexBar.Core.csproj package`.

- [ ] **Step 3: Commit**

```bash
git add src/windows/CodexBar.Core/CodexBar.Core.csproj
git commit -m "Add System.Management dependency for process inspection"
```

---

## Task 4: Implement the usage mapper (pure)

**Files:**
- Create: `src/windows/CodexBar.Core/Providers/Antigravity/AntigravityUsageMapper.cs`
- Test: `src/windows/CodexBar.Tests/AntigravityUsageMapperTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `src/windows/CodexBar.Tests/AntigravityUsageMapperTests.cs`:

```csharp
using System.Text.Json;
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Antigravity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class AntigravityUsageMapperTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [TestMethod]
    public void MapsQuotaSummaryToThreeLanes()
    {
        var root = Parse("""
        {
          "groups": [
            { "displayName": "Models", "buckets": [
              { "bucketId": "claude", "displayName": "Claude Sonnet", "remainingFraction": 0.25, "resetTime": "2030-01-01T00:00:00Z", "disabled": false },
              { "bucketId": "gemini-pro", "displayName": "Gemini 3 Pro", "remainingFraction": 0.80, "resetTime": "2030-01-01T00:00:00Z", "disabled": false },
              { "bucketId": "gemini-flash", "displayName": "Gemini Flash", "remainingFraction": 1.0, "resetTime": "2030-01-01T00:00:00Z", "disabled": false }
            ] }
          ]
        }
        """);

        var snapshot = AntigravityUsageMapper.Map("RetrieveUserQuotaSummary", root, DateTimeOffset.UnixEpoch);

        Assert.AreEqual(UsageProvider.Antigravity, snapshot.Provider);
        Assert.AreEqual(3, snapshot.Windows.Count);
        var claude = snapshot.Windows.Single(w => w.Title == "Claude");
        Assert.AreEqual(75.0, claude.UsedPercent, 0.001);
        var pro = snapshot.Windows.Single(w => w.Title == "Gemini Pro");
        Assert.AreEqual(20.0, pro.UsedPercent, 0.001);
        Assert.IsNull(snapshot.ErrorMessage);
    }

    [TestMethod]
    public void SkipsDisabledBuckets()
    {
        var root = Parse("""
        {
          "groups": [
            { "buckets": [
              { "bucketId": "claude", "displayName": "Claude", "remainingFraction": 0.5, "disabled": true }
            ] }
          ]
        }
        """);

        var snapshot = AntigravityUsageMapper.Map("RetrieveUserQuotaSummary", root, DateTimeOffset.UnixEpoch);

        Assert.AreEqual(0, snapshot.Windows.Count);
        Assert.AreEqual("Limits not available", snapshot.ErrorMessage);
        Assert.IsTrue(snapshot.IsStale);
    }

    [TestMethod]
    public void MapsUserStatusLanesPlanAndEmail()
    {
        var root = Parse("""
        {
          "accountEmail": "jim@example.com",
          "userStatus": {
            "userTier": { "preferredName": "Google AI Ultra" },
            "cascadeModelConfigData": {
              "clientModelConfigs": [
                { "label": "Claude Opus", "modelOrAlias": { "model": "claude-opus" }, "quotaInfo": { "remainingFraction": 0.10, "resetTime": "2030-01-01T00:00:00Z" } },
                { "label": "Gemini 3 Pro", "modelOrAlias": { "model": "gemini-3-pro" }, "quotaInfo": { "remainingFraction": 0.90, "resetTime": "2030-01-01T00:00:00Z" } }
              ]
            }
          }
        }
        """);

        var snapshot = AntigravityUsageMapper.Map("GetUserStatus", root, DateTimeOffset.UnixEpoch);

        Assert.AreEqual("jim@example.com", snapshot.AccountEmail);
        Assert.AreEqual("Google AI Ultra", snapshot.Plan);
        Assert.AreEqual(90.0, snapshot.Windows.Single(w => w.Title == "Claude").UsedPercent, 0.001);
        Assert.AreEqual(10.0, snapshot.Windows.Single(w => w.Title == "Gemini Pro").UsedPercent, 0.001);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --filter "FullyQualifiedName~AntigravityUsageMapperTests" --verbosity minimal`
Expected: FAIL — `AntigravityUsageMapper` does not exist (compile error).

- [ ] **Step 3: Implement the mapper**

Create `src/windows/CodexBar.Core/Providers/Antigravity/AntigravityUsageMapper.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Antigravity;

public static class AntigravityUsageMapper
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    public static UsageSnapshot Map(string method, JsonElement root, DateTimeOffset updatedAt) =>
        method == "RetrieveUserQuotaSummary"
            ? Build(QuotaSummaryBuckets(root), plan: null, email: null, updatedAt)
            : Build(UserStatusBuckets(root), UserStatusPlan(root), UserStatusEmail(root), updatedAt);

    private sealed record Bucket(string Identity, double? RemainingFraction, DateTimeOffset? ResetsAt, bool Disabled);

    private static UsageSnapshot Build(IEnumerable<Bucket> buckets, string? plan, string? email, DateTimeOffset updatedAt)
    {
        var list = buckets.Where(b => !b.Disabled && b.RemainingFraction is not null).ToArray();
        var windows = new List<RateWindow>(3);
        AddLane(windows, list, "claude", "Claude", s => s.Contains("claude", OIC));
        AddLane(windows, list, "gemini-pro", "Gemini Pro", s => s.Contains("gemini", OIC) && s.Contains("pro", OIC));
        AddLane(windows, list, "gemini-flash", "Gemini Flash", s => s.Contains("gemini", OIC) && s.Contains("flash", OIC));

        return new UsageSnapshot(
            UsageProvider.Antigravity,
            "Antigravity",
            updatedAt,
            windows,
            Clean(email),
            Clean(plan),
            null, null, null, null, null,
            "local",
            windows.Count == 0 ? "Limits not available" : null,
            windows.Count == 0);
    }

    private static void AddLane(
        ICollection<RateWindow> windows,
        IEnumerable<Bucket> buckets,
        string id,
        string title,
        Func<string, bool> matches)
    {
        var bucket = buckets
            .Where(b => matches(b.Identity))
            .OrderBy(b => b.RemainingFraction)
            .FirstOrDefault();
        if (bucket?.RemainingFraction is null)
        {
            return;
        }

        windows.Add(new RateWindow(
            id,
            title,
            Math.Round(Math.Clamp((1.0 - bucket.RemainingFraction.Value) * 100.0, 0.0, 100.0), 2),
            bucket.ResetsAt,
            null));
    }

    private static IEnumerable<Bucket> QuotaSummaryBuckets(JsonElement root)
    {
        if (!root.TryGetProperty("groups", out var groups) || groups.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var group in groups.EnumerateArray())
        {
            if (!group.TryGetProperty("buckets", out var buckets) || buckets.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var b in buckets.EnumerateArray())
            {
                var identity = $"{ReadString(b, "bucketId")} {ReadString(b, "displayName")}";
                yield return new Bucket(
                    identity,
                    ReadDouble(b, "remainingFraction"),
                    ReadResetTime(b, "resetTime"),
                    ReadBool(b, "disabled"));
            }
        }
    }

    private static IEnumerable<Bucket> UserStatusBuckets(JsonElement root)
    {
        if (!root.TryGetProperty("userStatus", out var userStatus) ||
            !userStatus.TryGetProperty("cascadeModelConfigData", out var data) ||
            !data.TryGetProperty("clientModelConfigs", out var configs) ||
            configs.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var config in configs.EnumerateArray())
        {
            var model = config.TryGetProperty("modelOrAlias", out var moa) ? ReadString(moa, "model") : null;
            var identity = $"{ReadString(config, "label")} {model}";
            double? remaining = null;
            DateTimeOffset? resets = null;
            if (config.TryGetProperty("quotaInfo", out var quotaInfo) && quotaInfo.ValueKind == JsonValueKind.Object)
            {
                remaining = ReadDouble(quotaInfo, "remainingFraction");
                resets = ReadResetTime(quotaInfo, "resetTime");
            }

            yield return new Bucket(identity, remaining, resets, Disabled: false);
        }
    }

    private static string? UserStatusPlan(JsonElement root)
    {
        if (!root.TryGetProperty("userStatus", out var userStatus))
        {
            return ReadString(root, "planName");
        }

        if (userStatus.TryGetProperty("userTier", out var tier) && tier.ValueKind == JsonValueKind.Object)
        {
            var name = ReadString(tier, "preferredName");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        if (userStatus.TryGetProperty("planStatus", out var planStatus) &&
            planStatus.TryGetProperty("planInfo", out var planInfo))
        {
            return ReadString(planInfo, "preferredName");
        }

        return ReadString(root, "planName");
    }

    private static string? UserStatusEmail(JsonElement root)
    {
        var email = ReadString(root, "accountEmail");
        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        return root.TryGetProperty("userStatus", out var userStatus)
            ? ReadString(userStatus, "accountEmail")
            : null;
    }

    private static DateTimeOffset? ReadResetTime(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return DateTimeOffset.TryParse(property.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed.ToUniversalTime()
                : null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static double? ReadDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetDouble(out var value)
            ? value
            : null;

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.True;

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --filter "FullyQualifiedName~AntigravityUsageMapperTests" --verbosity minimal`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/windows/CodexBar.Core/Providers/Antigravity/AntigravityUsageMapper.cs src/windows/CodexBar.Tests/AntigravityUsageMapperTests.cs
git commit -m "Add Antigravity usage mapper"
```

---

## Task 5: Define the process-locator interface

**Files:**
- Create: `src/windows/CodexBar.Core/Providers/Antigravity/IAntigravityProcessLocator.cs`

- [ ] **Step 1: Create the interface and candidate record**

Create `src/windows/CodexBar.Core/Providers/Antigravity/IAntigravityProcessLocator.cs`:

```csharp
namespace CodexBar.Core.Providers.Antigravity;

/// <summary>
/// One discovered Antigravity language-server endpoint. <see cref="CsrfToken"/> is empty for the
/// <c>agy</c> CLI (which requires no token) and the IDE token otherwise.
/// </summary>
public sealed record AntigravityCandidate(
    int Pid,
    IReadOnlyList<int> LoopbackPorts,
    string CsrfToken,
    int? ExtensionServerPort,
    string? ExtensionServerCsrfToken,
    bool IsCli);

public interface IAntigravityProcessLocator
{
    /// <summary>Returns every running Antigravity language server found, best candidate first.</summary>
    IReadOnlyList<AntigravityCandidate> FindCandidates();
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `C:\tmp\dotnet\dotnet.exe build src\windows\CodexBar.Core\CodexBar.Core.csproj --configuration Release --verbosity minimal`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/windows/CodexBar.Core/Providers/Antigravity/IAntigravityProcessLocator.cs
git commit -m "Add Antigravity process locator interface"
```

---

## Task 6: Implement the language-server client

**Files:**
- Create: `src/windows/CodexBar.Core/Providers/Antigravity/AntigravityLanguageServerClient.cs`
- Test: `src/windows/CodexBar.Tests/AntigravityLanguageServerClientTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `src/windows/CodexBar.Tests/AntigravityLanguageServerClientTests.cs`:

```csharp
using System.Net;
using CodexBar.Core.Providers.Antigravity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class AntigravityLanguageServerClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;
        public List<HttpRequestMessage> Requests { get; } = [];

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => this.responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private static AntigravityCandidate Candidate(string token = "tok") =>
        new(Pid: 1, LoopbackPorts: [42100], CsrfToken: token, ExtensionServerPort: null, ExtensionServerCsrfToken: null, IsCli: false);

    [TestMethod]
    public async Task SendsCsrfHeaderAndConnectVersion_AndReturnsFirstSuccess()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"groups":[]}""")
        });
        using var http = new HttpClient(handler);
        var client = new AntigravityLanguageServerClient(http);

        using var response = await client.FetchAsync(Candidate(), CancellationToken.None);

        Assert.IsNotNull(response);
        Assert.AreEqual("RetrieveUserQuotaSummary", response!.Method);
        var first = handler.Requests[0];
        Assert.IsTrue(first.RequestUri!.AbsoluteUri.EndsWith("/exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary", StringComparison.Ordinal));
        Assert.AreEqual("tok", first.Headers.GetValues("X-Codeium-Csrf-Token").Single());
        Assert.AreEqual("1", first.Headers.GetValues("Connect-Protocol-Version").Single());
    }

    [TestMethod]
    public async Task FallsBackToGetUserStatusWhenSummaryFails()
    {
        using var handler = new StubHandler(request =>
        {
            var ok = request.RequestUri!.AbsoluteUri.EndsWith("GetUserStatus", StringComparison.Ordinal);
            return new HttpResponseMessage(ok ? HttpStatusCode.OK : HttpStatusCode.NotFound)
            {
                Content = new StringContent(ok ? """{"userStatus":{}}""" : "nope")
            };
        });
        using var http = new HttpClient(handler);
        var client = new AntigravityLanguageServerClient(http);

        using var response = await client.FetchAsync(Candidate(), CancellationToken.None);

        Assert.IsNotNull(response);
        Assert.AreEqual("GetUserStatus", response!.Method);
    }

    [TestMethod]
    public async Task ReturnsNullWhenAllMethodsFail()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var http = new HttpClient(handler);
        var client = new AntigravityLanguageServerClient(http);

        var response = await client.FetchAsync(Candidate(), CancellationToken.None);

        Assert.IsNull(response);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --filter "FullyQualifiedName~AntigravityLanguageServerClientTests" --verbosity minimal`
Expected: FAIL — `AntigravityLanguageServerClient` does not exist (compile error).

- [ ] **Step 3: Implement the client**

Create `src/windows/CodexBar.Core/Providers/Antigravity/AntigravityLanguageServerClient.cs`:

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodexBar.Core.Providers.Antigravity;

/// <summary>One quota-bearing RPC response. Owns the parsed document; dispose after mapping.</summary>
public sealed class AntigravityQuotaResponse : IDisposable
{
    public AntigravityQuotaResponse(string method, JsonDocument document)
    {
        Method = method;
        Document = document;
    }

    public string Method { get; }
    public JsonDocument Document { get; }
    public void Dispose() => Document.Dispose();
}

public sealed class AntigravityLanguageServerClient
{
    private const string ServicePath = "/exa.language_server_pb.LanguageServerService/";
    private static readonly string[] Schemes = ["https", "http"];

    // Method name -> request body. Ordered: summary first, then legacy fallbacks.
    private static readonly (string Method, string Body)[] Rpcs =
    [
        ("RetrieveUserQuotaSummary", """{"forceRefresh":true}"""),
        ("GetUserStatus", """{"metadata":{"ideName":"antigravity","extensionName":"antigravity","ideVersion":"unknown","locale":"en"}}"""),
        ("GetCommandModelConfigs", """{"metadata":{"ideName":"antigravity","extensionName":"antigravity","ideVersion":"unknown","locale":"en"}}"""),
    ];

    private readonly HttpClient httpClient;

    public AntigravityLanguageServerClient(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<AntigravityQuotaResponse?> FetchAsync(AntigravityCandidate candidate, CancellationToken cancellationToken)
    {
        foreach (var port in candidate.LoopbackPorts)
        {
            foreach (var scheme in Schemes)
            {
                foreach (var (method, body) in Rpcs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var document = await TryPostAsync(scheme, port, method, body, candidate.CsrfToken, cancellationToken);
                    if (document is not null)
                    {
                        return new AntigravityQuotaResponse(method, document);
                    }
                }
            }
        }

        return null;
    }

    private async Task<JsonDocument?> TryPostAsync(
        string scheme,
        int port,
        string method,
        string body,
        string csrfToken,
        CancellationToken cancellationToken)
    {
        var uri = new Uri($"{scheme}://127.0.0.1:{port}{ServicePath}{method}");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.TryAddWithoutValidation("X-Codeium-Csrf-Token", csrfToken);
            request.Headers.TryAddWithoutValidation("Connect-Protocol-Version", "1");
            request.Content = new StringContent(body, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --filter "FullyQualifiedName~AntigravityLanguageServerClientTests" --verbosity minimal`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/windows/CodexBar.Core/Providers/Antigravity/AntigravityLanguageServerClient.cs src/windows/CodexBar.Tests/AntigravityLanguageServerClientTests.cs
git commit -m "Add Antigravity language server client"
```

---

## Task 7: Implement the provider orchestration

**Files:**
- Create: `src/windows/CodexBar.Core/Providers/Antigravity/AntigravityProvider.cs`
- Test: `src/windows/CodexBar.Tests/AntigravityProviderTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `src/windows/CodexBar.Tests/AntigravityProviderTests.cs`:

```csharp
using System.Net;
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Antigravity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class AntigravityProviderTests
{
    private sealed class FakeLocator(IReadOnlyList<AntigravityCandidate> candidates) : IAntigravityProcessLocator
    {
        public IReadOnlyList<AntigravityCandidate> FindCandidates() => candidates;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private static AntigravityCandidate Cli() =>
        new(Pid: 1, LoopbackPorts: [42100], CsrfToken: "", ExtensionServerPort: null, ExtensionServerCsrfToken: null, IsCli: true);

    [TestMethod]
    public async Task ReturnsNotRunningWhenNoCandidates()
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var provider = new AntigravityProvider(http, new FakeLocator([]));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(0, snapshot.Windows.Count);
        Assert.AreEqual("Antigravity isn't running.", snapshot.ErrorMessage);
    }

    [TestMethod]
    public async Task ReturnsNotAvailableWhenCandidatesButNoQuota()
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var provider = new AntigravityProvider(http, new FakeLocator([Cli()]));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual("Antigravity isn't available.", snapshot.ErrorMessage);
    }

    [TestMethod]
    public async Task MapsQuotaWhenServerResponds()
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {"groups":[{"buckets":[
              {"bucketId":"gemini-pro","displayName":"Gemini 3 Pro","remainingFraction":0.4,"resetTime":"2030-01-01T00:00:00Z","disabled":false}
            ]}]}
            """)
        }));
        var provider = new AntigravityProvider(http, new FakeLocator([Cli()]));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(UsageProvider.Antigravity, snapshot.Provider);
        Assert.AreEqual(60.0, snapshot.Windows.Single(w => w.Title == "Gemini Pro").UsedPercent, 0.001);
        Assert.IsNull(snapshot.ErrorMessage);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --filter "FullyQualifiedName~AntigravityProviderTests" --verbosity minimal`
Expected: FAIL — `AntigravityProvider` does not exist (compile error).

- [ ] **Step 3: Implement the provider**

Create `src/windows/CodexBar.Core/Providers/Antigravity/AntigravityProvider.cs`:

```csharp
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Antigravity;

public sealed class AntigravityProvider : IUsageProvider
{
    private readonly AntigravityLanguageServerClient client;
    private readonly IAntigravityProcessLocator locator;

    public AntigravityProvider(HttpClient httpClient, IAntigravityProcessLocator locator)
    {
        client = new AntigravityLanguageServerClient(httpClient);
        this.locator = locator;
    }

    public UsageProvider Provider => UsageProvider.Antigravity;

    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        var candidates = locator.FindCandidates();
        if (candidates.Count == 0)
        {
            return Missing("Antigravity isn't running.");
        }

        foreach (var candidate in candidates)
        {
            using var response = await client.FetchAsync(candidate, cancellationToken);
            if (response is not null)
            {
                return AntigravityUsageMapper.Map(response.Method, response.Document.RootElement, DateTimeOffset.Now);
            }
        }

        return Missing("Antigravity isn't available.");
    }

    private static UsageSnapshot Missing(string message) =>
        UsageSnapshot.MissingCredentials(UsageProvider.Antigravity, "Antigravity", message);
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --filter "FullyQualifiedName~AntigravityProviderTests" --verbosity minimal`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/windows/CodexBar.Core/Providers/Antigravity/AntigravityProvider.cs src/windows/CodexBar.Tests/AntigravityProviderTests.cs
git commit -m "Add Antigravity provider orchestration"
```

---

## Task 8: Implement the Windows process locator

This task talks to live Windows APIs (WMI + iphlpapi) and cannot be unit-tested without a running Antigravity. It is verified manually in Task 11. Build-verify only here.

**Files:**
- Create: `src/windows/CodexBar.Core/Providers/Antigravity/WindowsAntigravityProcessLocator.cs`

- [ ] **Step 1: Implement the locator**

Create `src/windows/CodexBar.Core/Providers/Antigravity/WindowsAntigravityProcessLocator.cs`:

```csharp
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace CodexBar.Core.Providers.Antigravity;

/// <summary>
/// Discovers the running Antigravity language server by inspecting process command lines (WMI)
/// and mapping each match to its loopback listening ports (iphlpapi). Windows-only.
/// </summary>
public sealed partial class WindowsAntigravityProcessLocator : IAntigravityProcessLocator
{
    [GeneratedRegex(@"(^|[/\\])language[_-]server([_-][a-z0-9]+)*(\.exe)?(\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex LanguageServerPattern();

    [GeneratedRegex(@"(^|[/\\])(antigravity[-_]cli|agy)(\.exe)?(\s|$|[/\\])", RegexOptions.IgnoreCase)]
    private static partial Regex CliPattern();

    [GeneratedRegex(@"--csrf_token[=\s]+(\S+)")]
    private static partial Regex CsrfPattern();

    [GeneratedRegex(@"--extension_server_port[=\s]+(\d+)")]
    private static partial Regex ExtensionPortPattern();

    [GeneratedRegex(@"--extension_server_csrf_token[=\s]+(\S+)")]
    private static partial Regex ExtensionCsrfPattern();

    public IReadOnlyList<AntigravityCandidate> FindCandidates()
    {
        var candidates = new List<AntigravityCandidate>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, Name, ExecutablePath, CommandLine FROM Win32_Process");
            foreach (var item in searcher.Get())
            {
                using var process = item;
                var commandLine = process["CommandLine"] as string ?? string.Empty;
                var exePath = process["ExecutablePath"] as string ?? string.Empty;
                var haystack = $"{exePath} {commandLine}";

                var isCli = CliPattern().IsMatch(haystack);
                var isLanguageServer = LanguageServerPattern().IsMatch(haystack) && HasAntigravityMarker(commandLine);
                if (!isCli && !isLanguageServer)
                {
                    continue;
                }

                var pid = Convert.ToInt32(process["ProcessId"]);
                var ports = ListeningLoopbackPorts(pid);
                if (ports.Count == 0)
                {
                    continue;
                }

                var csrf = Match(CsrfPattern(), commandLine);
                // The agy CLI requires no token; the IDE language server does. Skip IDE matches
                // that expose no token — there is nothing we can authenticate with.
                if (!isCli && string.IsNullOrEmpty(csrf))
                {
                    continue;
                }

                var extPortText = Match(ExtensionPortPattern(), commandLine);
                int? extPort = int.TryParse(extPortText, out var p) ? p : null;

                candidates.Add(new AntigravityCandidate(
                    pid,
                    ports,
                    csrf ?? string.Empty,
                    extPort,
                    Match(ExtensionCsrfPattern(), commandLine),
                    isCli));
            }
        }
        catch (ManagementException)
        {
            // WMI unavailable or query rejected — treat as "not found".
        }

        // CLI candidates (empty-token, simplest path) first.
        return candidates.OrderByDescending(c => c.IsCli).ToList();
    }

    private static bool HasAntigravityMarker(string commandLine) =>
        commandLine.Contains("--app_data_dir antigravity", StringComparison.OrdinalIgnoreCase) ||
        commandLine.Contains("antigravity", StringComparison.OrdinalIgnoreCase);

    private static string? Match(Regex pattern, string text)
    {
        var match = pattern.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    // --- iphlpapi: list the loopback TCP ports a given PID is LISTENING on ---

    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_LISTENER = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public uint owningPid;
    }

    [LibraryImport("iphlpapi.dll", SetLastError = true)]
    private static partial uint GetExtendedTcpTable(
        IntPtr pTcpTable, ref int pdwSize, [MarshalAs(UnmanagedType.Bool)] bool bOrder,
        int ulAf, int tableClass, uint reserved);

    private static IReadOnlyList<int> ListeningLoopbackPorts(int pid)
    {
        var ports = new List<int>();
        int size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0);
        if (size == 0)
        {
            return ports;
        }

        var table = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(table, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0) != 0)
            {
                return ports;
            }

            int count = Marshal.ReadInt32(table);
            var rowPtr = table + 4;
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            for (int i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                rowPtr += rowSize;
                if (row.owningPid != (uint)pid)
                {
                    continue;
                }

                if (!new IPAddress(row.localAddr).Equals(IPAddress.Loopback))
                {
                    continue;
                }

                // localPort is the port in network byte order packed into the low word.
                int port = ((int)(row.localPort & 0xFF) << 8) | (int)((row.localPort >> 8) & 0xFF);
                ports.Add(port);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(table);
        }

        return ports;
    }
}
```

> Note: this covers IPv4 loopback (`127.0.0.1`), which is what the Codeium-derived server binds. IPv6 loopback (`::1`) is intentionally not enumerated in v1.

- [ ] **Step 2: Build to verify it compiles**

Run: `C:\tmp\dotnet\dotnet.exe build src\windows\CodexBar.Core\CodexBar.Core.csproj --configuration Release --verbosity minimal`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/windows/CodexBar.Core/Providers/Antigravity/WindowsAntigravityProcessLocator.cs
git commit -m "Add Windows Antigravity process locator"
```

---

## Task 9: Wire the loopback HttpClient and register the provider

**Files:**
- Modify: `src/windows/CodexBar.WinUI/AppHostBuilder.cs`

- [ ] **Step 1: Add the using and the loopback client field**

In `src/windows/CodexBar.WinUI/AppHostBuilder.cs`, add the using with the other provider usings (after line 13):
```csharp
using CodexBar.Core.Providers.Antigravity;
```

Add a property next to `HttpClient` (after line 27):
```csharp
    public HttpClient AntigravityHttpClient { get; }
```

- [ ] **Step 2: Construct the loopback client in the AppShell ctor**

In the `AppShell` constructor, immediately after `HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };` (line 97), add:
```csharp
        // Dedicated client that trusts the Antigravity language server's self-signed loopback cert.
        // Kept separate from HttpClient so this bypass never applies to any other provider.
        AntigravityHttpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, _) =>
                request.RequestUri?.Host is "127.0.0.1" or "::1" or "localhost"
        })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
```

- [ ] **Step 3: Register the provider in BuildProviders**

In `BuildProviders` (after the Copilot line, line 152), add:
```csharp
        if (settings.AntigravityEnabled) list.Add(new AntigravityProvider(AntigravityHttpClient, new WindowsAntigravityProcessLocator()));
```

- [ ] **Step 4: Dispose the loopback client**

In `Dispose()` (after `HttpClient.Dispose();`, line 142), add:
```csharp
        AntigravityHttpClient.Dispose();
```

- [ ] **Step 5: Build + run the full suite**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --verbosity minimal`
Expected: Build succeeded; all tests pass (including the three new Antigravity test classes and the settings round-trip).

- [ ] **Step 6: Commit**

```bash
git add src/windows/CodexBar.WinUI/AppHostBuilder.cs
git commit -m "Register Antigravity provider with loopback HttpClient"
```

---

## Task 10: Gemini deprecation message

**Files:**
- Modify: `src/windows/CodexBar.Core/Providers/Gemini/GeminiProvider.cs`
- Test: `src/windows/CodexBar.Tests/GeminiProviderTests.cs`

- [ ] **Step 1: Write the failing test**

Open `src/windows/CodexBar.Tests/GeminiProviderTests.cs`, read its existing helpers (how it writes a temp `oauth_creds.json`, its `TestAppPaths`/`IAppPaths` stub, and its `QueueHandler`). Add a test that drives the 401 path through `RefreshAsync` and asserts the new message. Use the file's existing credential-writing helper and HTTP-stub helper — names below mirror the patterns seen in `ClaudeProviderTests.cs`; adjust to the actual helper names in `GeminiProviderTests.cs`:

```csharp
    [TestMethod]
    public async Task ReturnsRetirementMessageWhenQuotaCallIsForbidden()
    {
        // Valid, non-expired credentials so the provider proceeds to the quota call.
        var credentialsPath = await WriteCredentialsFileAsync("""
        {
          "access_token": "live-token",
          "refresh_token": "refresh",
          "expiry_date": 99999999999999
        }
        """);
        using var handler = new QueueHandler(
            new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden));
        using var httpClient = new HttpClient(handler);
        var provider = new GeminiProvider(httpClient, new TestAppPaths(credentialsPath));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        StringAssert.Contains(snapshot.ErrorMessage, "retired June 18, 2026");
        StringAssert.Contains(snapshot.ErrorMessage, "Antigravity");
    }
```

If `GeminiProviderTests.cs` has no temp-credential or queue helper, copy the `QueueHandler` and the temp-file/`TestAppPaths` helpers from `ClaudeProviderTests.cs` into this test file.

- [ ] **Step 2: Run to verify it fails**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --filter "FullyQualifiedName~ReturnsRetirementMessageWhenQuotaCallIsForbidden" --verbosity minimal`
Expected: FAIL — currently the 401/403 path throws `AuthenticationRequiredException` with the old "sign-in expired" message, so the assertion on the new text fails.

- [ ] **Step 3: Change the deprecation message**

In `src/windows/CodexBar.Core/Providers/Gemini/GeminiProvider.cs`, `PostJsonAsync`, replace the `Unauthorized or Forbidden` branch (lines 146–147):

```csharp
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new GeminiRetiredException(
                "Gemini CLI was retired June 18, 2026. Your Gemini usage now appears under Antigravity. (Paid Gemini Code Assist licenses are unaffected — re-run `gemini` to reconnect.)");
```

Add a private exception type and catch it in `RefreshAsync` so it renders as a status, not a crash. At the top of `RefreshAsync` (line 29), wrap the existing body's quota calls: change the two `PostJsonAsync` calls region so a `GeminiRetiredException` is converted to a `Missing(...)` snapshot. Concretely, wrap lines 75–90 in try/catch:

```csharp
        try
        {
            using var load = await PostJsonAsync(
                LoadCodeAssistUri,
                credentials.AccessToken!,
                LoadCodeAssistRequest(null),
                cancellationToken);
            var project = ReadString(load.RootElement, "cloudaicompanionProject")
                ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")
                ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT_ID");

            using var quota = await PostJsonAsync(
                RetrieveUserQuotaUri,
                credentials.AccessToken!,
                string.IsNullOrWhiteSpace(project) ? new { } : new { project },
                cancellationToken);

            return GeminiUsageMapper.Map(load.RootElement, quota.RootElement, credentials.Email, DateTimeOffset.Now);
        }
        catch (GeminiRetiredException ex)
        {
            return Missing(ex.Message);
        }
```

Add the exception type at the bottom of the file, after the `GeminiProvider` class closes (before the `IGeminiOAuthClientProvider` interface):

```csharp
internal sealed class GeminiRetiredException(string message) : Exception(message);
```

> Rationale: paid Code Assist licenses still authenticate, so a true auth-required state is no longer the common case. The retirement message is informational and points the user to the Antigravity card. The existing `AuthenticationRequiredException` import/usage elsewhere is left intact.

- [ ] **Step 4: Run to verify it passes**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --filter "FullyQualifiedName~GeminiProviderTests" --verbosity minimal`
Expected: PASS — the new test passes and existing Gemini tests still pass. If an existing test asserted the old "sign-in expired" 401 message, update that test's expected string to the new message (the behavior intentionally changed).

- [ ] **Step 5: Commit**

```bash
git add src/windows/CodexBar.Core/Providers/Gemini/GeminiProvider.cs src/windows/CodexBar.Tests/GeminiProviderTests.cs
git commit -m "Show Gemini CLI retirement message and point to Antigravity"
```

---

## Task 11: Docs, full suite, and manual verification

**Files:**
- Create: `docs/windows-antigravity.md`

- [ ] **Step 1: Write the setup doc**

Create `docs/windows-antigravity.md`:

```markdown
# Antigravity (Windows)

CodexBar reads Claude, Gemini Pro, and Gemini Flash quota from the local Antigravity
language server. This replaces the Gemini CLI path, which Google retired on June 18, 2026.

## Requirements

- Install Antigravity (the `agy` CLI or the Antigravity IDE) and sign in with your Google account.
- Antigravity must be **running** for CodexBar to read quota. The CLI's language server runs
  while `agy` is active; the IDE's runs while the IDE is open.

## How it works

CodexBar finds the running Antigravity language server on a loopback port and calls its local
quota RPC. No tokens are stored by CodexBar; for the IDE it reads the CSRF token from the running
process, and the `agy` CLI requires no token.

## Troubleshooting

- **"Antigravity isn't running."** — Start `agy` or the Antigravity IDE, then refresh.
- **"Antigravity isn't available."** — The server was found but did not return quota. Make sure
  you are signed in (`agy` login), then refresh.
- **"Limits not available."** — You are signed in but the server reported no quota buckets yet.
  Use Antigravity once, then refresh.
```

- [ ] **Step 2: Run the full Windows test suite**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --verbosity minimal`
Expected: All tests pass.

- [ ] **Step 3: Manual verification against a running Antigravity**

This is the only check for `WindowsAntigravityProcessLocator` (it cannot be unit-tested).

1. Build/run the app per the README/AGENTS build path, or package with `.\Scripts\package-windows.ps1` and launch the portable build.
2. Start `agy` (or open the Antigravity IDE) and confirm you are signed in.
3. Open the CodexBar popover and select the Antigravity card.
   - Expected: Claude / Gemini Pro / Gemini Flash lanes with percentages, plus your plan tier.
4. Quit `agy` / close the IDE and refresh.
   - Expected: "Antigravity isn't running."
5. Open the Gemini card with no working Gemini CLI auth.
   - Expected: "Gemini CLI was retired June 18, 2026. Your Gemini usage now appears under Antigravity."

Record which binary was validated (per AGENTS.md handoff guidance).

- [ ] **Step 4: Commit the doc**

```bash
git add docs/windows-antigravity.md
git commit -m "Add Antigravity Windows setup doc"
```

- [ ] **Step 5: Update CHANGELOG**

Add an entry under the current `0.25 — Preview` → `### Windows` section in `CHANGELOG.md`:

```markdown
- Antigravity: add a local provider that reads Claude + Gemini Pro + Gemini Flash quota from the running Antigravity language server, replacing the retired consumer Gemini CLI path.
- Gemini: report the June 18, 2026 Gemini CLI retirement and point users to the Antigravity card.
```

Commit:
```bash
git add CHANGELOG.md
git commit -m "Changelog: Antigravity provider and Gemini retirement notice"
```

---

## Self-Review

**Spec coverage:**
- All-lane card (Claude + Gemini Pro + Gemini Flash) + tier + email → Tasks 4, 7 (mapper builds three lanes, reads plan/email).
- Local LS discovery (process, port, CSRF, CLI vs IDE) → Tasks 5, 8.
- RPC chain + headers + http/https + loopback TLS → Tasks 6, 9.
- Enabled-by-default setting + toggle → Task 2.
- Not-running / not-available / limits-not-available states → Tasks 4 (limits), 7 (running/available).
- Keep Gemini provider, add retirement message → Task 10.
- Wiring (enum, settings, links, registration, UI) → Tasks 1, 2, 9.
- Tests (mapper, client, provider, Gemini message) → Tasks 4, 6, 7, 10.
- Docs → Task 11.
- Out of scope honored: no account switching, no Credential Manager read, no other-provider changes.

**Placeholder scan:** No TBD/TODO; every code step shows complete code; every test step shows the assertions; every command shows expected output.

**Type consistency:** `AntigravityCandidate`, `IAntigravityProcessLocator.FindCandidates()`, `AntigravityLanguageServerClient.FetchAsync(...)` → `AntigravityQuotaResponse?`, `AntigravityUsageMapper.Map(string method, JsonElement root, DateTimeOffset)`, and `UsageProvider.Antigravity` are used identically across Tasks 4–9. `UsageSnapshot.MissingCredentials(provider, displayName, message)` matches the real factory signature. `AppSettings` argument order in `ToSettings()` (Task 2) matches the record field order edited in the same task.

**Known judgment calls flagged for the implementer:**
- `System.Management` 9.0.5 version may need bumping to the resolvable 9.0.x (Task 3 step 2).
- `GeminiProviderTests.cs` helper names may differ from `ClaudeProviderTests.cs`; reuse whatever that file already defines (Task 10 step 1).
- IPv4 loopback only in the locator (Task 8 note).
