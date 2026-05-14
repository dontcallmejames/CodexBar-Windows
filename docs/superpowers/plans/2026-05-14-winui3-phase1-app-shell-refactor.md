# WinUI 3 Migration Phase 1 — App Shell Refactor

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Decompose the 1075-line `App.xaml.cs` into focused orchestrator services, add per-provider adaptive backoff with a visible freshness indicator, and prepare the codebase to host either the current WPF shell or a new WinUI 3 shell against the same `CodexBar.Core`.

**Architecture:** Pull lifecycle concerns out of the WPF `Application` subclass into named services owned by a single `AppShellController`. Move all refresh/update-timer/window-coordination state out of `App`. Replace the global "isRefreshing" flag with per-provider state inside `CodexBar.Core` so the new freshness indicator and backoff work the same in WPF today and WinUI 3 tomorrow. Wire everything via `Microsoft.Extensions.Hosting`.

**Tech Stack:** .NET 9, C# (nullable, latest lang), WPF (current shell, transitional), `Microsoft.Extensions.Hosting` 9.x, `Microsoft.Extensions.DependencyInjection` 9.x, MSTest, existing `CodexBar.Core` / `CodexBar.Tray`.

**Build command:** `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --verbosity minimal`

**Out of scope:** Any WinUI 3 code (that is Phase 2). Any UI/UX changes beyond a small "Updated 12s ago" status line that the existing popover ViewModel already supports binding to.

---

## File Structure

**Create:**
- `src/windows/CodexBar.Core/Refresh/ProviderRefreshState.cs` — per-provider tracked state (last success, last error, consecutive failures, next-allowed-at)
- `src/windows/CodexBar.Core/Refresh/AdaptiveBackoff.cs` — pure function: failures → delay
- `src/windows/CodexBar.Core/Refresh/RateLimitException.cs` — typed exception so providers can signal 429/503 without parsing strings later
- `src/windows/CodexBar.WinApp/Services/RefreshOrchestrator.cs` — owns refresh timer, dispatches per-provider refresh respecting backoff
- `src/windows/CodexBar.WinApp/Services/UpdateNotifier.cs` — owns update-check timer, single-fire-per-tag notification logic
- `src/windows/CodexBar.WinApp/Services/WindowCoordinator.cs` — owns popover/dock/settings/firstrun/about windows + positioning
- `src/windows/CodexBar.WinApp/Services/TrayController.cs` — owns `TrayIconHost` + snapshot→tray mapping
- `src/windows/CodexBar.WinApp/Services/AppShellController.cs` — top-level orchestrator; replaces most of `App.xaml.cs`
- `src/windows/CodexBar.WinApp/Services/IShellWindow.cs` — minimal contract over `System.Windows.Window` so coordinator tests don't need a Dispatcher
- `src/windows/CodexBar.Tests/RefreshOrchestratorTests.cs`
- `src/windows/CodexBar.Tests/UpdateNotifierTests.cs`
- `src/windows/CodexBar.Tests/AdaptiveBackoffTests.cs`
- `src/windows/CodexBar.Tests/ProviderRefreshStateTests.cs`
- `src/windows/CodexBar.Tests/AppShellControllerTests.cs`

**Modify:**
- `src/windows/CodexBar.Core/Refresh/RefreshScheduler.cs` — accept `ProviderRefreshState` registry, surface per-provider results, classify exceptions
- `src/windows/CodexBar.Core/Providers/Claude/ClaudeProvider.cs:140-160` — throw `RateLimitException` from `ThrowIfRateLimited`
- `src/windows/CodexBar.Core/Providers/Codex/CodexProvider.cs` — throw `RateLimitException` for 429/503
- `src/windows/CodexBar.Core/Providers/Gemini/GeminiProvider.cs` — throw `RateLimitException` for 429/503
- `src/windows/CodexBar.WinApp/ViewModels/PopoverViewModel.cs` — surface `LastUpdatedRelative` per active provider
- `src/windows/CodexBar.WinApp/App.xaml.cs` — shrink to host bootstrap + thin event forwarding (~80 lines)
- `src/windows/CodexBar.WinApp/AppServices.cs` — expose `ProviderRefreshState` registry, accept `ILogger`
- `src/windows/CodexBar.WinApp/CodexBar.WinApp.csproj` — add Hosting / DI / Logging packages

---

## Baseline

- [ ] **Step 0.1: Verify clean baseline**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --verbosity minimal`
Expected: All tests pass. Record count for later comparison.

- [ ] **Step 0.2: Commit baseline marker (no code change)**

```bash
git commit --allow-empty -m "chore: baseline before Phase 1 app-shell refactor"
```

---

### Task 1: Adaptive backoff (pure logic, no dependencies)

**Files:**
- Create: `src/windows/CodexBar.Core/Refresh/AdaptiveBackoff.cs`
- Create: `src/windows/CodexBar.Tests/AdaptiveBackoffTests.cs`

- [ ] **Step 1.1: Write failing tests**

```csharp
// src/windows/CodexBar.Tests/AdaptiveBackoffTests.cs
using CodexBar.Core.Refresh;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class AdaptiveBackoffTests
{
    [TestMethod]
    public void ZeroFailures_ReturnsZero()
    {
        Assert.AreEqual(TimeSpan.Zero, AdaptiveBackoff.Delay(0));
    }

    [TestMethod]
    public void Failures_ExponentialWithJitter_Cap()
    {
        var oneFail = AdaptiveBackoff.Delay(1);
        var threeFails = AdaptiveBackoff.Delay(3);
        var manyFails = AdaptiveBackoff.Delay(20);

        Assert.IsTrue(oneFail >= TimeSpan.FromSeconds(15) && oneFail <= TimeSpan.FromSeconds(45));
        Assert.IsTrue(threeFails >= TimeSpan.FromMinutes(1));
        Assert.IsTrue(manyFails <= TimeSpan.FromMinutes(30), "cap at 30 minutes");
    }

    [TestMethod]
    public void RateLimitHint_HonorsRetryAfter()
    {
        var delay = AdaptiveBackoff.Delay(1, retryAfter: TimeSpan.FromSeconds(120));
        Assert.IsTrue(delay >= TimeSpan.FromSeconds(120));
    }
}
```

- [ ] **Step 1.2: Run tests, verify FAIL**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --filter FullyQualifiedName~AdaptiveBackoffTests`
Expected: FAIL — `AdaptiveBackoff` does not exist.

- [ ] **Step 1.3: Implement**

```csharp
// src/windows/CodexBar.Core/Refresh/AdaptiveBackoff.cs
namespace CodexBar.Core.Refresh;

public static class AdaptiveBackoff
{
    private static readonly TimeSpan Cap = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(20);

    public static TimeSpan Delay(int consecutiveFailures, TimeSpan? retryAfter = null)
    {
        if (consecutiveFailures <= 0 && retryAfter is null)
        {
            return TimeSpan.Zero;
        }

        var jitter = 1.0 + (Random.Shared.NextDouble() * 0.5 - 0.25);
        var exponential = Base.TotalSeconds * Math.Pow(2, Math.Max(0, consecutiveFailures - 1)) * jitter;
        var computed = TimeSpan.FromSeconds(Math.Min(exponential, Cap.TotalSeconds));
        return retryAfter is { } hint && hint > computed ? hint : computed;
    }
}
```

- [ ] **Step 1.4: Run tests, verify PASS**

Run: same as 1.2.
Expected: PASS.

- [ ] **Step 1.5: Commit**

```bash
git add src/windows/CodexBar.Core/Refresh/AdaptiveBackoff.cs src/windows/CodexBar.Tests/AdaptiveBackoffTests.cs
git commit -m "Add AdaptiveBackoff for per-provider refresh delay"
```

---

### Task 2: ProviderRefreshState registry

**Files:**
- Create: `src/windows/CodexBar.Core/Refresh/ProviderRefreshState.cs`
- Create: `src/windows/CodexBar.Tests/ProviderRefreshStateTests.cs`

- [ ] **Step 2.1: Write failing tests**

```csharp
using CodexBar.Core.Models;
using CodexBar.Core.Refresh;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class ProviderRefreshStateTests
{
    [TestMethod]
    public void NewProvider_HasNoLastSuccess_AndIsDue()
    {
        var registry = new ProviderRefreshStateRegistry(() => DateTimeOffset.UnixEpoch);
        var state = registry.Get(UsageProvider.Codex);
        Assert.IsNull(state.LastSuccess);
        Assert.IsTrue(state.IsDue(DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void RecordSuccess_ResetsFailures_AndUpdatesTimestamp()
    {
        var clock = DateTimeOffset.Parse("2026-05-14T12:00:00Z");
        var registry = new ProviderRefreshStateRegistry(() => clock);
        registry.RecordFailure(UsageProvider.Codex, retryAfter: null);
        registry.RecordSuccess(UsageProvider.Codex);
        var state = registry.Get(UsageProvider.Codex);
        Assert.AreEqual(0, state.ConsecutiveFailures);
        Assert.AreEqual(clock, state.LastSuccess);
    }

    [TestMethod]
    public void RecordFailure_BlocksNextRefreshForBackoffWindow()
    {
        var clock = DateTimeOffset.Parse("2026-05-14T12:00:00Z");
        var registry = new ProviderRefreshStateRegistry(() => clock);
        registry.RecordFailure(UsageProvider.Codex, retryAfter: TimeSpan.FromMinutes(2));
        var state = registry.Get(UsageProvider.Codex);
        Assert.IsFalse(state.IsDue(clock.AddSeconds(30)));
        Assert.IsTrue(state.IsDue(clock.AddMinutes(3)));
    }
}
```

- [ ] **Step 2.2: Run tests, verify FAIL**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --filter FullyQualifiedName~ProviderRefreshStateTests`
Expected: FAIL — type does not exist.

- [ ] **Step 2.3: Implement**

```csharp
// src/windows/CodexBar.Core/Refresh/ProviderRefreshState.cs
using System.Collections.Concurrent;
using CodexBar.Core.Models;

namespace CodexBar.Core.Refresh;

public sealed record ProviderRefreshState(
    DateTimeOffset? LastSuccess,
    DateTimeOffset? LastAttempt,
    int ConsecutiveFailures,
    DateTimeOffset? NextAllowedAt,
    string? LastErrorMessage)
{
    public static ProviderRefreshState Empty { get; } =
        new(null, null, 0, null, null);

    public bool IsDue(DateTimeOffset now) =>
        NextAllowedAt is null || now >= NextAllowedAt;
}

public sealed class ProviderRefreshStateRegistry
{
    private readonly Func<DateTimeOffset> clock;
    private readonly ConcurrentDictionary<UsageProvider, ProviderRefreshState> states = new();

    public ProviderRefreshStateRegistry(Func<DateTimeOffset>? clock = null)
    {
        this.clock = clock ?? (() => DateTimeOffset.Now);
    }

    public ProviderRefreshState Get(UsageProvider provider) =>
        states.TryGetValue(provider, out var state) ? state : ProviderRefreshState.Empty;

    public void RecordAttempt(UsageProvider provider)
    {
        var now = clock();
        states.AddOrUpdate(provider,
            _ => ProviderRefreshState.Empty with { LastAttempt = now },
            (_, prev) => prev with { LastAttempt = now });
    }

    public void RecordSuccess(UsageProvider provider)
    {
        var now = clock();
        states[provider] = new ProviderRefreshState(
            LastSuccess: now,
            LastAttempt: now,
            ConsecutiveFailures: 0,
            NextAllowedAt: null,
            LastErrorMessage: null);
    }

    public void RecordFailure(UsageProvider provider, string? message = null, TimeSpan? retryAfter = null)
    {
        var now = clock();
        states.AddOrUpdate(provider,
            _ => Build(now, 1, message, retryAfter),
            (_, prev) => Build(now, prev.ConsecutiveFailures + 1, message, retryAfter, prev.LastSuccess));
    }

    private static ProviderRefreshState Build(
        DateTimeOffset now,
        int failures,
        string? message,
        TimeSpan? retryAfter,
        DateTimeOffset? lastSuccess = null) =>
        new(lastSuccess, now, failures, now + AdaptiveBackoff.Delay(failures, retryAfter), message);
}
```

- [ ] **Step 2.4: Run tests, verify PASS**

Expected: PASS.

- [ ] **Step 2.5: Commit**

```bash
git add src/windows/CodexBar.Core/Refresh/ProviderRefreshState.cs src/windows/CodexBar.Tests/ProviderRefreshStateTests.cs
git commit -m "Add ProviderRefreshStateRegistry for per-provider tracking"
```

---

### Task 3: RateLimitException + provider integration

**Files:**
- Create: `src/windows/CodexBar.Core/Refresh/RateLimitException.cs`
- Modify: `src/windows/CodexBar.Core/Providers/Claude/ClaudeProvider.cs` (replace string-throw in `ThrowIfRateLimited`)
- Modify: `src/windows/CodexBar.Core/Providers/Codex/CodexProvider.cs`
- Modify: `src/windows/CodexBar.Core/Providers/Gemini/GeminiProvider.cs`
- Modify: `src/windows/CodexBar.Tests/ClaudeProviderTests.cs` — add test asserting typed exception + Retry-After parsing

- [ ] **Step 3.1: Write failing test in ClaudeProviderTests**

```csharp
[TestMethod]
public async Task RateLimited_ThrowsTypedExceptionWithRetryAfter()
{
    var handler = new FakeHandler((req) =>
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests);
        response.Headers.Add("Retry-After", "120");
        return response;
    });
    var client = new HttpClient(handler);
    var provider = new ClaudeProvider(client, FakePaths.WithClaudeOAuth());

    var ex = await Assert.ThrowsExceptionAsync<RateLimitException>(
        () => provider.RefreshAsync(default));

    Assert.AreEqual(TimeSpan.FromSeconds(120), ex.RetryAfter);
}
```

(Reuse existing `FakeHandler` / `FakePaths` from `ClaudeProviderTests.cs`; if names differ, match what's there.)

- [ ] **Step 3.2: Run, verify FAIL**

Expected: FAIL — `RateLimitException` does not exist.

- [ ] **Step 3.3: Implement RateLimitException**

```csharp
// src/windows/CodexBar.Core/Refresh/RateLimitException.cs
namespace CodexBar.Core.Refresh;

public sealed class RateLimitException : Exception
{
    public RateLimitException(string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan? RetryAfter { get; }
}
```

- [ ] **Step 3.4: Wire it into the three providers**

In `ClaudeProvider.cs` replace the body of `ThrowIfRateLimited`:

```csharp
private static void ThrowIfRateLimited(HttpResponseMessage response)
{
    if (response.StatusCode is not (System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.ServiceUnavailable))
    {
        return;
    }

    TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta
        ?? (response.Headers.RetryAfter?.Date is { } date
            ? date - DateTimeOffset.Now
            : null);

    throw new RateLimitException(
        $"Claude API rate-limited with status {(int)response.StatusCode}.",
        retryAfter);
}
```

Apply the same pattern to `CodexProvider` and `GeminiProvider` wherever they currently surface 429/503 as a generic exception or string.

- [ ] **Step 3.5: Run all tests, verify PASS**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --verbosity minimal`
Expected: All pre-existing tests + new one PASS.

- [ ] **Step 3.6: Commit**

```bash
git add src/windows/CodexBar.Core src/windows/CodexBar.Tests
git commit -m "Surface RateLimitException with Retry-After from providers"
```

---

### Task 4: RefreshScheduler honors registry + backoff

**Files:**
- Modify: `src/windows/CodexBar.Core/Refresh/RefreshScheduler.cs`
- Modify: `src/windows/CodexBar.WinApp/AppServices.cs` (expose registry)
- Create: `src/windows/CodexBar.Tests/RefreshSchedulerBackoffTests.cs`

- [ ] **Step 4.1: Write failing tests**

```csharp
using CodexBar.Core.Models;
using CodexBar.Core.Providers;
using CodexBar.Core.Refresh;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class RefreshSchedulerBackoffTests
{
    [TestMethod]
    public async Task RateLimitedProvider_SkipsNextCycle_UntilBackoffElapses()
    {
        var clock = DateTimeOffset.Parse("2026-05-14T12:00:00Z");
        var current = clock;
        var registry = new ProviderRefreshStateRegistry(() => current);
        var store = new SnapshotStore();
        var provider = new FakeProvider(UsageProvider.Codex, throwRateLimited: true);
        var scheduler = new RefreshScheduler(new[] { (IUsageProvider)provider }, store, registry);

        await scheduler.RefreshAllAsync(default);
        Assert.AreEqual(1, provider.CallCount);

        current = clock.AddSeconds(10);
        await scheduler.RefreshAllAsync(default);
        Assert.AreEqual(1, provider.CallCount, "still inside backoff window");

        current = clock.AddMinutes(5);
        await scheduler.RefreshAllAsync(default);
        Assert.AreEqual(2, provider.CallCount, "past backoff, retried");
    }
}
```

Define `FakeProvider` as a private nested helper in the same file (throws `RateLimitException(retryAfter: TimeSpan.FromMinutes(2))` when configured, otherwise returns a healthy snapshot).

- [ ] **Step 4.2: Run, FAIL**

Expected: FAIL — `RefreshScheduler` constructor signature does not accept registry.

- [ ] **Step 4.3: Update `RefreshScheduler`**

```csharp
public sealed class RefreshScheduler
{
    private readonly IReadOnlyList<IUsageProvider> providers;
    private readonly SnapshotStore store;
    private readonly ProviderRefreshStateRegistry registry;
    private readonly Func<DateTimeOffset> clock;

    public RefreshScheduler(
        IReadOnlyList<IUsageProvider> providers,
        SnapshotStore store,
        ProviderRefreshStateRegistry? registry = null,
        Func<DateTimeOffset>? clock = null)
    {
        this.providers = providers;
        this.store = store;
        this.registry = registry ?? new ProviderRefreshStateRegistry(clock);
        this.clock = clock ?? (() => DateTimeOffset.Now);
    }

    public ProviderRefreshStateRegistry Registry => registry;

    public async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        var now = clock();
        foreach (var provider in providers)
        {
            if (!registry.Get(provider.Provider).IsDue(now))
            {
                continue;
            }

            registry.RecordAttempt(provider.Provider);
            try
            {
                var snapshot = await provider.RefreshAsync(cancellationToken);
                store.Set(snapshot);
                registry.RecordSuccess(provider.Provider);
            }
            catch (RateLimitException rl)
            {
                registry.RecordFailure(provider.Provider, rl.Message, rl.RetryAfter);
                store.Set(FailedSnapshot(provider, rl.Message));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                registry.RecordFailure(provider.Provider, error.Message);
                store.Set(FailedSnapshot(provider, error.Message));
            }
        }
    }

    private UsageSnapshot FailedSnapshot(IUsageProvider provider, string message)
    {
        var previous = store.Get(provider.Provider);
        return previous is null
            ? UsageSnapshot.MissingCredentials(provider.Provider, provider.Provider.ToString(), message)
            : previous with { IsStale = true, ErrorMessage = message };
    }
}
```

- [ ] **Step 4.4: Update `AppServices`**

In `AppServices.cs`, store the registry alongside the scheduler:

```csharp
RefreshStates = new ProviderRefreshStateRegistry();
Scheduler = new RefreshScheduler(Providers, Store, RefreshStates);
```

Add public property:

```csharp
public ProviderRefreshStateRegistry RefreshStates { get; }
```

- [ ] **Step 4.5: Run all tests, verify PASS**

Expected: PASS (existing scheduler tests must still pass — verify the new `IsDue` check doesn't regress them).

- [ ] **Step 4.6: Commit**

```bash
git add src/windows/CodexBar.Core src/windows/CodexBar.Tests src/windows/CodexBar.WinApp/AppServices.cs
git commit -m "Honor per-provider backoff in RefreshScheduler"
```

---

### Task 5: Add Hosting + DI packages

**Files:**
- Modify: `src/windows/CodexBar.WinApp/CodexBar.WinApp.csproj`

- [ ] **Step 5.1: Edit csproj**

Add inside an `<ItemGroup>`:

```xml
<PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="9.0.0" />
```

- [ ] **Step 5.2: Run build**

Run: `C:\tmp\dotnet\dotnet.exe build src\windows\CodexBar.Windows.sln --configuration Release`
Expected: SUCCESS.

- [ ] **Step 5.3: Commit**

```bash
git add src/windows/CodexBar.WinApp/CodexBar.WinApp.csproj
git commit -m "Add Microsoft.Extensions.Hosting packages"
```

---

### Task 6: Extract RefreshOrchestrator

Responsibility: own the `DispatcherTimer`, call `Scheduler.RefreshAllAsync`, raise an event when snapshots change so listeners (tray, popover, dock) can update.

**Files:**
- Create: `src/windows/CodexBar.WinApp/Services/RefreshOrchestrator.cs`
- Create: `src/windows/CodexBar.Tests/RefreshOrchestratorTests.cs`

- [ ] **Step 6.1: Write failing test**

```csharp
using CodexBar.Core.Refresh;
using CodexBar.WinApp.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class RefreshOrchestratorTests
{
    [TestMethod]
    public async Task ManualRefresh_InvokesScheduler_AndRaisesEvent()
    {
        var fake = new FakeScheduler();
        var orchestrator = new RefreshOrchestrator(fake, () => TimeSpan.FromMinutes(5));
        var raised = 0;
        orchestrator.Refreshed += (_, _) => raised++;

        await orchestrator.RefreshNowAsync(default);

        Assert.AreEqual(1, fake.Calls);
        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public async Task ConcurrentRefresh_Coalesces()
    {
        var fake = new FakeScheduler { Delay = TimeSpan.FromMilliseconds(50) };
        var orchestrator = new RefreshOrchestrator(fake, () => TimeSpan.FromMinutes(5));

        var t1 = orchestrator.RefreshNowAsync(default);
        var t2 = orchestrator.RefreshNowAsync(default);
        await Task.WhenAll(t1, t2);

        Assert.AreEqual(1, fake.Calls, "second call should be coalesced");
    }

    private sealed class FakeScheduler : IRefreshScheduler
    {
        public int Calls;
        public TimeSpan Delay;
        public async Task RefreshAllAsync(CancellationToken ct)
        {
            Calls++;
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
        }
    }
}
```

- [ ] **Step 6.2: Run, FAIL**

Expected: FAIL — types don't exist.

- [ ] **Step 6.3: Implement**

```csharp
// src/windows/CodexBar.WinApp/Services/RefreshOrchestrator.cs
using CodexBar.Core.Refresh;

namespace CodexBar.WinApp.Services;

public interface IRefreshScheduler
{
    Task RefreshAllAsync(CancellationToken cancellationToken);
}

public sealed class RefreshOrchestrator : IDisposable
{
    private readonly IRefreshScheduler scheduler;
    private readonly Func<TimeSpan> intervalProvider;
    private readonly SemaphoreSlim gate = new(1, 1);
    private System.Windows.Threading.DispatcherTimer? timer;

    public event EventHandler? Refreshed;

    public RefreshOrchestrator(IRefreshScheduler scheduler, Func<TimeSpan> intervalProvider)
    {
        this.scheduler = scheduler;
        this.intervalProvider = intervalProvider;
    }

    public void Start()
    {
        Stop();
        timer = new System.Windows.Threading.DispatcherTimer { Interval = intervalProvider() };
        timer.Tick += async (_, _) => await RefreshNowAsync(default);
        timer.Start();
    }

    public void Stop()
    {
        timer?.Stop();
        timer = null;
    }

    public void RestartWithCurrentInterval()
    {
        if (timer is null) return;
        timer.Interval = intervalProvider();
    }

    public async Task RefreshNowAsync(CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            return;
        }
        try
        {
            await scheduler.RefreshAllAsync(cancellationToken);
            Refreshed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        Stop();
        gate.Dispose();
    }
}
```

Make `RefreshScheduler` implement `IRefreshScheduler` (it already has the matching method — just add `: IRefreshScheduler` to its declaration).

- [ ] **Step 6.4: Run tests, verify PASS**

Expected: PASS.

- [ ] **Step 6.5: Commit**

```bash
git add src/windows/CodexBar.WinApp/Services/RefreshOrchestrator.cs src/windows/CodexBar.Core/Refresh/RefreshScheduler.cs src/windows/CodexBar.Tests/RefreshOrchestratorTests.cs
git commit -m "Extract RefreshOrchestrator with coalescing"
```

---

### Task 7: Extract UpdateNotifier

Responsibility: own update-check timer, dedupe notifications by tag, expose `LatestResult`.

**Files:**
- Create: `src/windows/CodexBar.WinApp/Services/UpdateNotifier.cs`
- Create: `src/windows/CodexBar.Tests/UpdateNotifierTests.cs`

- [ ] **Step 7.1: Write failing tests**

```csharp
using CodexBar.WinApp.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class UpdateNotifierTests
{
    [TestMethod]
    public async Task NotifiesOncePerTag()
    {
        var checker = new FakeUpdateChecker
        {
            Result = new UpdateCheckResult(
                CurrentVersion: "0.25",
                LatestVersion: "0.26",
                LatestTag: "v0.26",
                IsUpdateAvailable: true,
                ReleaseUrl: new Uri("https://example/r"),
                CheckedAt: DateTimeOffset.UnixEpoch)
        };
        var notifications = new List<UpdateCheckResult>();
        var notifier = new UpdateNotifier(checker, r => notifications.Add(r));

        await notifier.CheckNowAsync(default);
        await notifier.CheckNowAsync(default);

        Assert.AreEqual(1, notifications.Count);
    }

    [TestMethod]
    public async Task NewerTag_NotifiesAgain()
    {
        var checker = new FakeUpdateChecker();
        var notifications = new List<UpdateCheckResult>();
        var notifier = new UpdateNotifier(checker, r => notifications.Add(r));

        checker.Result = new UpdateCheckResult("0.25","0.26","v0.26",true,new Uri("https://x/1"),DateTimeOffset.UnixEpoch);
        await notifier.CheckNowAsync(default);
        checker.Result = new UpdateCheckResult("0.25","0.27","v0.27",true,new Uri("https://x/2"),DateTimeOffset.UnixEpoch);
        await notifier.CheckNowAsync(default);

        Assert.AreEqual(2, notifications.Count);
    }

    private sealed class FakeUpdateChecker : IUpdateChecker
    {
        public UpdateCheckResult? Result;
        public Task<UpdateCheckResult?> CheckAsync(CancellationToken ct) => Task.FromResult(Result);
    }
}
```

(`UpdateCheckResult` and `IUpdateChecker` already exist; use the real types from `CodexBar.WinApp/UpdateChecker.cs`.)

- [ ] **Step 7.2: FAIL**

- [ ] **Step 7.3: Implement**

```csharp
// src/windows/CodexBar.WinApp/Services/UpdateNotifier.cs
namespace CodexBar.WinApp.Services;

public sealed class UpdateNotifier : IDisposable
{
    private readonly IUpdateChecker checker;
    private readonly Action<UpdateCheckResult> onUpdateAvailable;
    private readonly SemaphoreSlim gate = new(1, 1);
    private System.Windows.Threading.DispatcherTimer? timer;
    private string? lastNotifiedTag;

    public UpdateCheckResult? LatestResult { get; private set; }
    public event EventHandler? ResultChanged;

    public UpdateNotifier(IUpdateChecker checker, Action<UpdateCheckResult> onUpdateAvailable)
    {
        this.checker = checker;
        this.onUpdateAvailable = onUpdateAvailable;
    }

    public void Start(TimeSpan interval)
    {
        Stop();
        timer = new System.Windows.Threading.DispatcherTimer { Interval = interval };
        timer.Tick += async (_, _) => await CheckNowAsync(default);
        timer.Start();
    }

    public void Stop()
    {
        timer?.Stop();
        timer = null;
    }

    public async Task CheckNowAsync(CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            var result = await checker.CheckAsync(cancellationToken);
            if (result is null) return;
            LatestResult = result;
            ResultChanged?.Invoke(this, EventArgs.Empty);
            if (result.IsUpdateAvailable && result.LatestTag != lastNotifiedTag)
            {
                lastNotifiedTag = result.LatestTag;
                onUpdateAvailable(result);
            }
        }
        finally { gate.Release(); }
    }

    public void Dispose() { Stop(); gate.Dispose(); }
}
```

- [ ] **Step 7.4: PASS**

- [ ] **Step 7.5: Commit**

```bash
git commit -am "Extract UpdateNotifier with per-tag dedup"
```

---

### Task 8: Extract TrayController

Responsibility: own `TrayIconHost`, translate snapshots → `TrayDisplayModel` (using the "most-constrained provider" rule).

**Files:**
- Create: `src/windows/CodexBar.WinApp/Services/TrayController.cs`
- Create: `src/windows/CodexBar.Tests/TrayControllerTests.cs`

- [ ] **Step 8.1: Write failing test**

```csharp
using CodexBar.Core.Models;
using CodexBar.WinApp.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class TrayControllerTests
{
    [TestMethod]
    public void MostConstrainedProvider_DrivesTrayBadge()
    {
        var snapshots = new[]
        {
            UsageSnapshot.Healthy(UsageProvider.Codex, "Codex", planLabel: "Pro", percentUsed: 0.30, /*…rest defaults*/),
            UsageSnapshot.Healthy(UsageProvider.Claude, "Claude", planLabel: "Pro", percentUsed: 0.81, /*…*/),
        };
        var model = TrayController.SelectDisplay(snapshots, showUsageAsUsed: true);
        StringAssert.Contains(model.Tooltip, "Claude");
        Assert.AreEqual(81, model.PercentUsed);
    }
}
```

(Use the actual `UsageSnapshot` factory members that exist in `CodexBar.Core.Models`; adjust signature to match.)

- [ ] **Step 8.2: FAIL**

- [ ] **Step 8.3: Implement**

Pull the existing tray-update logic out of `App.xaml.cs:UpdateTrayFromSnapshots`. Expose a static pure `TrayController.SelectDisplay` that returns the `TrayDisplayModel`, and keep the side-effecting `Apply` on the instance.

```csharp
// src/windows/CodexBar.WinApp/Services/TrayController.cs
using CodexBar.Core.Models;
using CodexBar.Tray;

namespace CodexBar.WinApp.Services;

public sealed class TrayController : IDisposable
{
    private readonly TrayIconHost host;

    public TrayController(TrayIconHost host)
    {
        this.host = host;
    }

    public void Apply(IReadOnlyList<UsageSnapshot> snapshots, bool showUsageAsUsed)
    {
        host.Update(SelectDisplay(snapshots, showUsageAsUsed));
    }

    public static TrayDisplayModel SelectDisplay(IReadOnlyList<UsageSnapshot> snapshots, bool showUsageAsUsed)
    {
        // existing logic moved verbatim from App.xaml.cs UpdateTrayFromSnapshots,
        // returning a TrayDisplayModel instead of mutating tray directly
    }

    public void Dispose() => host.Dispose();
}
```

- [ ] **Step 8.4: PASS**

- [ ] **Step 8.5: Commit**

---

### Task 9: Extract WindowCoordinator

Responsibility: own every `Window?` field currently on `App`, all positioning helpers, and the show/hide logic. Keep `App.xaml.cs` window-free.

**Files:**
- Create: `src/windows/CodexBar.WinApp/Services/WindowCoordinator.cs`
- Move (do not retype): All `Calculate*`, `Position*`, `Show*` methods related to popover/dock/settings/firstrun/about.

This is mechanical move + rename. The existing `WpfShellTests.cs` is the safety net.

- [ ] **Step 9.1: Move static positioning methods (already pure)**

Move these from `App.xaml.cs` to `WindowCoordinator` as `public static`:
- `CalculatePopoverPosition`
- `CalculatePopoverMaxHeight`
- `CalculatePopoverMaxHeightNearDock`
- `CalculatePopoverPositionNearDock`
- `CalculateTaskbarDockPosition`
- `CalculateRefreshInterval`
- `CalculateUpdateCheckInterval`

Update `WpfShellTests.cs` references to new namespace/class. Run tests: PASS.

- [ ] **Step 9.2: Move window lifecycle methods**

Cut from `App.xaml.cs` into `WindowCoordinator`:
- `ShowPopover`, `ShowPopoverWindow`, `WirePopoverWindowEvents`, `UnwirePopoverWindowEvents`, `Popover_*` handlers, `PositionPopoverNearCursor` (both overloads), `PositionPopoverNearDock`, `PositionTaskbarDock`, `ShowPopoverFromDock`, `HideTaskbarDock`
- `ShowSettings`, `ShowFirstRunOnboarding`, `WireFirstRunWindowEvents`, About-window methods

Constructor takes whatever services they need (`AppServices`, `RefreshOrchestrator`, callbacks for Quit/etc.).

- [ ] **Step 9.3: Build + run all tests**

Expected: PASS. If `WpfShellTests` needs DispatcherSetup, keep them in the existing fixture; they already drive WPF Dispatcher.

- [ ] **Step 9.4: Commit**

```bash
git commit -am "Extract WindowCoordinator from App"
```

---

### Task 10: Extract AppShellController + slim App.xaml.cs

**Files:**
- Create: `src/windows/CodexBar.WinApp/Services/AppShellController.cs`
- Modify: `src/windows/CodexBar.WinApp/App.xaml.cs` (target: ~80 lines)
- Create: `src/windows/CodexBar.Tests/AppShellControllerTests.cs`

- [ ] **Step 10.1: Write characterization test**

```csharp
[TestMethod]
public async Task Startup_RefreshesOnce_ThenStartsTimer_ThenShowsFirstRunIfNoSettings()
{
    var controller = AppShellControllerHarness.Build(settingsFileExists: false);
    await controller.StartAsync(default);
    Assert.IsTrue(controller.RefreshOrchestrator.RefreshCount >= 1);
    Assert.IsTrue(controller.RefreshOrchestrator.TimerStarted);
    Assert.IsTrue(controller.WindowCoordinator.FirstRunShown);
}
```

(Harness uses fakes for `IRefreshScheduler`, `IUpdateChecker`, `WindowCoordinator` test-double.)

- [ ] **Step 10.2: FAIL**

- [ ] **Step 10.3: Implement `AppShellController`**

```csharp
namespace CodexBar.WinApp.Services;

public sealed class AppShellController : IDisposable
{
    private readonly AppServices services;
    private readonly RefreshOrchestrator refresh;
    private readonly UpdateNotifier updates;
    private readonly TrayController tray;
    private readonly WindowCoordinator windows;
    private readonly IStartupRegistration startup;

    public AppShellController(
        AppServices services,
        RefreshOrchestrator refresh,
        UpdateNotifier updates,
        TrayController tray,
        WindowCoordinator windows,
        IStartupRegistration startup)
    { /* assign */ }

    public async Task StartAsync(CancellationToken ct)
    {
        ApplyStartupRegistration();
        await refresh.RefreshNowAsync(ct);
        tray.Apply(services.Store.All(), services.Settings.ShowUsageAsUsed);
        refresh.Refreshed += (_, _) =>
        {
            tray.Apply(services.Store.All(), services.Settings.ShowUsageAsUsed);
            windows.OnSnapshotsChanged();
        };
        refresh.Start();
        if (services.Settings.CheckForUpdatesAutomatically)
        {
            updates.Start(TimeSpan.FromHours(24));
        }
        if (ShouldShowFirstRun()) windows.ShowFirstRun();
    }

    public void Dispose() { refresh.Dispose(); updates.Dispose(); tray.Dispose(); windows.Dispose(); }
}
```

- [ ] **Step 10.4: Rewrite `App.xaml.cs`**

Target shape:

```csharp
public partial class App : System.Windows.Application
{
    private IHost? host;
    private AppShellController? controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        host = AppHostBuilder.Build();
        await host.StartAsync();
        controller = host.Services.GetRequiredService<AppShellController>();
        await controller.StartAsync(default);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        controller?.Dispose();
        if (host is not null) await host.StopAsync();
        host?.Dispose();
        base.OnExit(e);
    }
}
```

Add `AppHostBuilder` that registers `AppServices`, `RefreshOrchestrator`, `UpdateNotifier`, `WindowCoordinator`, `TrayController`, `AppShellController` in DI.

- [ ] **Step 10.5: Run full test suite**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --verbosity minimal`
Expected: All pre-existing tests + new ones PASS.

- [ ] **Step 10.6: Manual smoke**

Launch `CodexBar.WinApp.exe`, verify: tray icon appears; left-click opens popover; settings opens; refresh occurs on schedule; quit cleanly shuts down without orphaned processes.

- [ ] **Step 10.7: Commit**

```bash
git commit -am "Introduce AppShellController; App.xaml.cs reduced to host bootstrap"
```

---

### Task 11: Expose "last updated" in popover

**Files:**
- Modify: `src/windows/CodexBar.WinApp/ViewModels/PopoverViewModel.cs`
- Modify: `src/windows/CodexBar.WinApp/Views/PopoverWindow.xaml`
- Modify: `src/windows/CodexBar.Tests/PopoverViewModelTests.cs`

- [ ] **Step 11.1: Add failing test**

```csharp
[TestMethod]
public void LiveIndicator_ShowsRelativeTime_FromRegistry()
{
    var registry = new ProviderRefreshStateRegistry(() => DateTimeOffset.Parse("2026-05-14T12:00:00Z"));
    registry.RecordSuccess(UsageProvider.Codex);
    var vm = new PopoverViewModel(/* existing args */, refreshStates: registry, now: DateTimeOffset.Parse("2026-05-14T12:00:12Z"));
    StringAssert.Contains(vm.LiveIndicatorText, "12s ago");
}
```

- [ ] **Step 11.2: Implement**

Add `refreshStates` parameter to `PopoverViewModel`, compute `LiveIndicatorText` as `"Live • updated {n}s/m/h ago"` from `registry.Get(ActiveProvider).LastSuccess`. Bind in `PopoverWindow.xaml` to a small Caption-sized TextBlock above the metrics block.

- [ ] **Step 11.3: Pass through wiring**

`WindowCoordinator.ShowPopoverWindow` should pass `services.RefreshStates` when building the ViewModel.

- [ ] **Step 11.4: Full test pass + manual smoke**

- [ ] **Step 11.5: Commit**

```bash
git commit -am "Show per-provider live update indicator in popover"
```

---

### Task 12: Final cleanup

- [ ] **Step 12.1: Sanity scan**

Run: `grep -nE "DispatcherTimer|isRefreshing|isCheckingUpdates|lastNotifiedUpdateTag" src/windows/CodexBar.WinApp/App.xaml.cs`
Expected: zero matches.

- [ ] **Step 12.2: Line count check**

Run: `wc -l src/windows/CodexBar.WinApp/App.xaml.cs`
Expected: under 120 lines.

- [ ] **Step 12.3: Final commit if needed**

```bash
git commit --allow-empty -m "chore: Phase 1 refactor complete"
```

---

## Self-Review Checklist (run after writing implementation)

- [ ] Every method previously on `App` is either deleted or now lives on `RefreshOrchestrator`, `UpdateNotifier`, `WindowCoordinator`, `TrayController`, or `AppShellController`.
- [ ] No test was deleted to "make it pass." Existing test count strictly increased.
- [ ] `RefreshScheduler` still has backwards-compatible constructor (registry is optional with default).
- [ ] Per-provider backoff and live indicator both visibly work on a real Windows build.
- [ ] No new dependencies beyond Hosting / DI / Logging packages.

---

## Execution Handoff

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between, fast iteration.
2. **Inline Execution** — execute tasks here with checkpoints.
