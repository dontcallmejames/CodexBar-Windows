# WinUI 3 Phase 3 — Parity & Cutover

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the WinUI 3 shell (`CodexBar.WinUI`) to feature parity with the WPF shell (`CodexBar.WinApp`), then promote it to the primary shell. WPF gets archived; WinUI is what the installer ships.

**Architecture:** Continue building on the Phase 2 spike branch. Port logic from `CodexBar.WinApp/Services/` (now stable Phase 1 code) into the WinUI app: the same `RefreshOrchestrator`, `UpdateNotifier`, `WindowCoordinator`-equivalent, and `AppShellController`-equivalent patterns. UI is rebuilt in WinUI 3 idiom using `CommunityToolkit.WinUI.Controls.SettingsControls`, `MenuFlyout`, `AppNotification`, and the existing `ThemeListener` / `PopoverPositioner` from Phase 2.

**Tech Stack:** .NET 9, Windows App SDK 1.6+, WinUI 3, C# (nullable, latest lang), `CommunityToolkit.Mvvm` 8.3+, `CommunityToolkit.WinUI.Controls.SettingsControls` 8.1+, `CommunityToolkit.WinUI.Notifications` (for AppNotification), MSTest, existing `CodexBar.Core`. Existing `CodexBar.Tray.MeterIconRenderer` is reused (System.Drawing.Icon production is shell-agnostic). The WPF `CodexBar.Tray.TrayIconHost` is replaced by a WinUI-native `TrayHost` using `H.NotifyIcon.WinUI` so we drop the WinForms NotifyIcon dependency entirely.

**Build command:** `C:\tmp\dotnet\dotnet.exe build src\windows\CodexBar.Windows.sln -c Release`
**Test command:** `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln -c Release --verbosity minimal`
**Run command:** `.\src\windows\CodexBar.WinUI\bin\Release\net9.0-windows10.0.22621.0\win-x64\CodexBar.WinUI.exe`

**Prerequisite:** Phase 2 spike branch (`claude/winui3-phase2-spike`) is merged or branched from. Phase 1 must be on `main`. Current spike commit referenced throughout: `2640ef53`.

**Out of scope:**
- SQLite snapshot history + sparklines → separate plan
- Windows 11 widget board entry → separate plan
- MSIX packaging + Microsoft Store submission → separate plan (when shell is feature-complete)

---

## File Structure

**Create:**
- `src/windows/CodexBar.WinUI/Services/TrayHost.cs` — H.NotifyIcon.WinUI wrapper, replaces ad-hoc TrayIconHost reuse
- `src/windows/CodexBar.WinUI/Services/SingleInstance.cs` — AppInstance.FindOrRegisterForKey wrapper
- `src/windows/CodexBar.WinUI/Services/AppShell.cs` — replaces inline AppHostBuilder.AppShell, owns full provider/scheduler/orchestrator graph (porting from `CodexBar.WinApp.Services.AppShellController`)
- `src/windows/CodexBar.WinUI/Services/RefreshOrchestrator.cs` — copy of WPF version, decoupled from `System.Windows.Threading.DispatcherTimer` (uses `DispatcherQueueTimer`)
- `src/windows/CodexBar.WinUI/Services/UpdateNotifier.cs` — copy of WPF version, same DispatcherQueueTimer change
- `src/windows/CodexBar.WinUI/Services/WindowCoordinator.cs` — replaces inline App.xaml.cs window management
- `src/windows/CodexBar.WinUI/Services/AppNotificationsBootstrap.cs` — registers `AppNotificationManager`
- `src/windows/CodexBar.WinUI/Services/CrashLogger.cs` — extracts existing inline crash log
- `src/windows/CodexBar.WinUI/ViewModels/SettingsViewModel.cs`
- `src/windows/CodexBar.WinUI/ViewModels/FirstRunViewModel.cs`
- `src/windows/CodexBar.WinUI/ViewModels/AboutViewModel.cs`
- `src/windows/CodexBar.WinUI/ViewModels/TaskbarDockViewModel.cs`
- `src/windows/CodexBar.WinUI/Views/SettingsWindow.xaml(.cs)`
- `src/windows/CodexBar.WinUI/Views/FirstRunWindow.xaml(.cs)`
- `src/windows/CodexBar.WinUI/Views/AboutWindow.xaml(.cs)`
- `src/windows/CodexBar.WinUI/Views/TaskbarDockWindow.xaml(.cs)`
- `src/windows/CodexBar.WinUI/Views/MainContextMenu.xaml` (MenuFlyout XAML reused as tray context)
- `src/windows/CodexBar.WinUI/Assets/codexbar.ico` — branded tray icon (copy from CodexBar.WinApp/Assets)
- `src/windows/CodexBar.Tests/WinUiSettingsViewModelTests.cs`
- `src/windows/CodexBar.Tests/WinUiRefreshOrchestratorTests.cs`
- `src/windows/CodexBar.Tests/WinUiSingleInstanceTests.cs`
- `Scripts/package-winui-installer.ps1` — Inno installer that bundles the WinUI build instead of WPF
- `installer/winui/CodexBarSetup.iss` — Inno script for the WinUI build

**Modify:**
- `src/windows/CodexBar.WinUI/CodexBar.WinUI.csproj` — add H.NotifyIcon.WinUI, CommunityToolkit.WinUI.Controls.SettingsControls, CommunityToolkit.WinUI.Notifications; remove ProjectReference to `CodexBar.Tray` (after MeterIconRenderer moves)
- `src/windows/CodexBar.WinUI/App.xaml.cs` — slim to ~50 lines (boot AppShell, hand off to controller pattern)
- `src/windows/CodexBar.WinUI/Views/PopoverWindow.xaml` — add real provider icons + colors, widen to fit 4 tabs, add footer actions row (Add Account / Dashboard / Status / Settings / About / Quit)
- `src/windows/CodexBar.WinUI/Views/PopoverWindow.xaml.cs` — wire footer commands to AppShell
- `src/windows/CodexBar.WinUI/ViewModels/PopoverViewModel.cs` — port footer commands from WPF VM
- `src/windows/CodexBar.WinUI/AppHostBuilder.cs` — replaced by `AppShell` service
- `src/windows/CodexBar.Tray/MeterIconRenderer.cs` — moves to `CodexBar.Core/Tray/MeterIconRenderer.cs` so WinUI uses it without WPF dependency

**Delete (in final cutover task only):**
- `src/windows/CodexBar.WinApp/**` (after cutover, archive under `legacy-wpf/` instead of delete)
- `src/windows/CodexBar.Tray/**` (after MeterIconRenderer extracted; the WinForms NotifyIcon wrapper becomes obsolete)

---

## Baseline

- [ ] **Step 0.1: Verify the spike branch is buildable and runnable**

Run: `C:\tmp\dotnet\dotnet.exe build src\windows\CodexBar.Windows.sln -c Release`
Expected: SUCCESS, 0 warnings.

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln -c Release --verbosity minimal`
Expected: 196 tests pass.

Launch the spike, click tray, see Acrylic popover with provider data. Confirm visual baseline.

- [ ] **Step 0.2: Mark the baseline**

```bash
git commit --allow-empty -m "chore: baseline before Phase 3 parity work"
```

---

### Task 1: Move MeterIconRenderer into Core so WinUI can use it without WinForms

**Files:**
- Create: `src/windows/CodexBar.Core/Tray/MeterIconRenderer.cs` (moved from `CodexBar.Tray/MeterIconRenderer.cs`)
- Modify: `src/windows/CodexBar.Tray/MeterIconRenderer.cs` (delete — class is moved)
- Modify: `src/windows/CodexBar.Tray/TrayIconHost.cs:1` — update `using` if it imported the old namespace
- Modify: `src/windows/CodexBar.WinApp/Services/TrayController.cs` and any other caller — update namespace

`MeterIconRenderer` currently lives in `CodexBar.Tray` namespace and uses `System.Drawing` (not WinForms). It's already framework-agnostic — moving it to Core unblocks WinUI from depending on `CodexBar.Tray` (which transitively pulls WinForms).

- [ ] **Step 1.1: Move the file**

```bash
git mv src/windows/CodexBar.Tray/MeterIconRenderer.cs src/windows/CodexBar.Core/Tray/MeterIconRenderer.cs
```

- [ ] **Step 1.2: Update namespace**

Edit `src/windows/CodexBar.Core/Tray/MeterIconRenderer.cs`. Change `namespace CodexBar.Tray;` to `namespace CodexBar.Core.Tray;`.

- [ ] **Step 1.3: Update callers**

Grep for `using CodexBar.Tray;` and `MeterIconRenderer.Render`. Update import statements:
- `src/windows/CodexBar.Tray/TrayIconHost.cs` — add `using CodexBar.Core.Tray;`
- `src/windows/CodexBar.WinApp/Services/TrayController.cs` — same
- Any test files

- [ ] **Step 1.4: Build + test**

Run: `C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln -c Release --verbosity minimal`
Expected: all tests pass (196), no regressions.

- [ ] **Step 1.5: Commit**

```bash
git commit -am "Move MeterIconRenderer into CodexBar.Core.Tray"
```

---

### Task 2: Branded tray icon in WinUI

**Files:**
- Modify: `src/windows/CodexBar.WinUI/Services/TrayHost.cs` (currently created in Phase 2 spike) — replace blue-square fallback with `MeterIconRenderer.Render`
- Modify: `src/windows/CodexBar.WinUI/App.xaml.cs` — call `tray.Update(new TrayDisplayModel(...))` whenever snapshots change (currently only called once at startup)

The current `TrayHost` (Phase 2 spike, file `src/windows/CodexBar.WinUI/Services/TrayHost.cs`) wraps the existing `CodexBar.Tray.TrayIconHost`. For Phase 3 it should call into the relocated `MeterIconRenderer` directly so we can drop the `CodexBar.Tray` reference later.

- [ ] **Step 2.1: Rewrite TrayHost**

Open `src/windows/CodexBar.WinUI/Services/TrayHost.cs`. Replace with:

```csharp
using CodexBar.Core.Tray;
using H.NotifyIcon;
using System;
using System.Drawing;

namespace CodexBar.WinUI.Services;

public sealed class TrayHost : IDisposable
{
    private readonly TaskbarIcon icon = new();
    private Icon? currentIcon;

    public event EventHandler? LeftClick;
    public event EventHandler? RightClick;

    public TrayHost()
    {
        icon.NoLeftClickDelay = true;
        icon.ToolTipText = "CodexBar";
        icon.LeftClickCommand = new RelayCommand(() => LeftClick?.Invoke(this, EventArgs.Empty));
        icon.RightClickCommand = new RelayCommand(() => RightClick?.Invoke(this, EventArgs.Empty));
    }

    public void Show() => icon.ForceCreate();

    public void Update(TrayDisplayModel model)
    {
        currentIcon?.Dispose();
        currentIcon = MeterIconRenderer.Render(model);
        icon.Icon = currentIcon;
        icon.ToolTipText = Truncate(model.Tooltip);
    }

    public void Dispose()
    {
        icon.Dispose();
        currentIcon?.Dispose();
    }

    private static string Truncate(string tooltip) =>
        tooltip.Length <= 63 ? tooltip : tooltip[..63];
}

internal sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action action;
    public RelayCommand(Action action) { this.action = action; }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
}
```

Add H.NotifyIcon.WinUI to csproj if not present:

```xml
<PackageReference Include="H.NotifyIcon.WinUI" Version="2.2.0" />
```

- [ ] **Step 2.2: Wire tray updates to refresh events**

In `src/windows/CodexBar.WinUI/App.xaml.cs`, after the existing `tray.Show()` call, replace the one-shot update with a subscription:

```csharp
shell.OnSnapshotsChanged += () => uiDispatcher.TryEnqueue(() =>
    tray.Update(TraySelector.Build(shell.Store.All())));
tray.Update(TraySelector.Build(shell.Store.All()));
```

Create `src/windows/CodexBar.WinUI/Services/TraySelector.cs`:

```csharp
using CodexBar.Core.Models;
using CodexBar.Core.Tray;
using System.Collections.Generic;
using System.Linq;

namespace CodexBar.WinUI.Services;

public static class TraySelector
{
    public static TrayDisplayModel Build(IReadOnlyList<UsageSnapshot> snapshots)
    {
        var primary = snapshots
            .SelectMany(snapshot => snapshot.Windows)
            .OrderByDescending(window => window.UsedPercent)
            .FirstOrDefault();
        var percent = primary?.UsedPercent ?? 0;
        var stale = snapshots.Count == 0 || snapshots.Any(snapshot => snapshot.IsStale);
        return new TrayDisplayModel("CodexBar", percent, stale);
    }
}
```

`shell.OnSnapshotsChanged` doesn't exist yet — Task 4 adds it. For now, set up a `Shell.RefreshScheduler.RefreshAllAsync` continuation that fires the event manually. If that's awkward, defer this step's wiring and just call `Update` once at startup; Task 4 hooks the live updates.

- [ ] **Step 2.3: Build and visually verify**

Build, launch. Expected: tray icon shows a branded badge (the meter glyph) instead of a blue square.

- [ ] **Step 2.4: Commit**

```bash
git commit -am "Use branded MeterIconRenderer for WinUI tray icon"
```

---

### Task 3: Widen popover and show all 4 provider tabs

**Files:**
- Modify: `src/windows/CodexBar.WinUI/Views/PopoverWindow.xaml.cs:27` — change `Resize(380, 480)` to `Resize(440, 520)` (or whatever fits 4 tabs).

Or switch from `NavigationView Top` to `TabView`:

- Modify: `src/windows/CodexBar.WinUI/Views/PopoverWindow.xaml` — replace `<controls:NavigationView>` block with `<controls:Pivot>` or `<controls:TabView>` whose tabs include `Codex/Claude/Cursor/Gemini`.

For the minimum change: just resize to 440×520 and confirm all 4 tabs render without the ⋯ overflow.

- [ ] **Step 3.1: Resize window**

Edit `src/windows/CodexBar.WinUI/Views/PopoverWindow.xaml.cs` line 27:

```csharp
AppWindow.Resize(new Windows.Graphics.SizeInt32(440, 520));
```

Also update the call site in `App.xaml.cs:TogglePopover` where `PopoverPositioner.CalculateForCursor(pt.X, pt.Y, 380, 480, ...)` is called — change `380, 480` to `440, 520`.

- [ ] **Step 3.2: Build and visually verify**

Launch. Expected: all 4 tabs (Codex, Claude, Cursor, Gemini) visible across the top — no overflow chevron.

If they still don't fit, reduce the NavigationView item padding via a `Style` in the XAML:

```xml
<controls:NavigationView ...>
    <controls:NavigationView.Resources>
        <Thickness x:Key="NavigationViewItemContentMargin">8,0,8,0</Thickness>
    </controls:NavigationView.Resources>
    ...
</controls:NavigationView>
```

- [ ] **Step 3.3: Commit**

```bash
git commit -am "Widen popover to 440x520 so all 4 provider tabs fit"
```

---

### Task 4: Port RefreshOrchestrator + UpdateNotifier to WinUI

**Files:**
- Create: `src/windows/CodexBar.WinUI/Services/RefreshOrchestrator.cs`
- Create: `src/windows/CodexBar.WinUI/Services/UpdateNotifier.cs`
- Create: `src/windows/CodexBar.Tests/WinUiRefreshOrchestratorTests.cs`
- Modify: `src/windows/CodexBar.WinUI/AppHostBuilder.cs` — instantiate the orchestrator and notifier inside `AppShell` (rename `AppShell` → `AppShellController` for consistency with WPF naming, or keep `AppShell`)

The WPF versions live at `src/windows/CodexBar.WinApp/Services/RefreshOrchestrator.cs` and `src/windows/CodexBar.WinApp/Services/UpdateNotifier.cs`. They use `System.Windows.Threading.DispatcherTimer`. WinUI needs `Microsoft.UI.Dispatching.DispatcherQueueTimer`.

- [ ] **Step 4.1: Copy WPF RefreshOrchestrator and adapt**

Create `src/windows/CodexBar.WinUI/Services/RefreshOrchestrator.cs`:

```csharp
using CodexBar.Core.Refresh;
using Microsoft.UI.Dispatching;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodexBar.WinUI.Services;

public sealed class RefreshOrchestrator : IDisposable
{
    private readonly IRefreshScheduler scheduler;
    private readonly Func<TimeSpan> intervalProvider;
    private readonly CancellationToken shutdownToken;
    private readonly SemaphoreSlim gate = new(1, 1);
    private DispatcherQueueTimer? timer;

    public event EventHandler? Refreshed;

    public RefreshOrchestrator(
        IRefreshScheduler scheduler,
        Func<TimeSpan> intervalProvider,
        CancellationToken shutdownToken)
    {
        this.scheduler = scheduler;
        this.intervalProvider = intervalProvider;
        this.shutdownToken = shutdownToken;
    }

    public void Start()
    {
        Stop();
        timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = intervalProvider();
        timer.Tick += async (_, _) => await SafeTickAsync();
        timer.Start();
    }

    public void Stop()
    {
        timer?.Stop();
        timer = null;
    }

    public async Task RefreshNowAsync(CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            await scheduler.RefreshAllAsync(cancellationToken);
            Refreshed?.Invoke(this, EventArgs.Empty);
        }
        finally { gate.Release(); }
    }

    private async Task SafeTickAsync()
    {
        try { await RefreshNowAsync(shutdownToken); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"RefreshOrchestrator: {ex}"); }
    }

    public void Dispose()
    {
        Stop();
        try { gate.Wait(TimeSpan.FromSeconds(5)); } catch { }
        gate.Dispose();
    }
}
```

- [ ] **Step 4.2: Copy WPF UpdateNotifier and adapt**

Create `src/windows/CodexBar.WinUI/Services/UpdateNotifier.cs`. Mirror the WPF version (look at `src/windows/CodexBar.WinApp/Services/UpdateNotifier.cs`) but use `DispatcherQueueTimer` instead of `DispatcherTimer`. Constructor takes `(IUpdateChecker, Action<UpdateCheckResult>, CancellationToken)` like the WPF one.

Note: `IUpdateChecker` lives in `CodexBar.WinApp`. We need to move it to `CodexBar.Core` so WinUI can use it. Sub-task:

```bash
git mv src/windows/CodexBar.WinApp/UpdateChecker.cs src/windows/CodexBar.Core/Updates/UpdateChecker.cs
```

Edit the moved file: change `namespace CodexBar.WinApp;` to `namespace CodexBar.Core.Updates;`. Grep callers, fix usings.

- [ ] **Step 4.3: Failing tests for RefreshOrchestrator**

Create `src/windows/CodexBar.Tests/WinUiRefreshOrchestratorTests.cs`. Port the existing `RefreshOrchestratorTests.cs` (5 tests including the dispose-while-running regression). Same logic; just replace `using CodexBar.WinApp.Services;` with `using CodexBar.WinUI.Services;` and ensure `IRefreshScheduler` is found.

Caveat: the WinUI orchestrator uses `DispatcherQueueTimer` which requires a dispatcher queue on the test thread. The tests only exercise `RefreshNowAsync` (not the timer), so `Start()` is never called and the dispatcher dependency stays inert. Verify by running the tests.

If the dispatcher-free test path doesn't work (e.g., the orchestrator's `Start()` is called by some test), add `Microsoft.UI.Dispatching.DispatcherQueueController` setup in a `[TestInitialize]`.

- [ ] **Step 4.4: Run, FAIL, implement, PASS**

Expected: 196 → 201 (5 new tests).

- [ ] **Step 4.5: Wire AppShell to start/stop both**

In `src/windows/CodexBar.WinUI/AppHostBuilder.cs`, change `AppShell` to construct and own the orchestrator + notifier. After `Scheduler` field, add:

```csharp
public RefreshOrchestrator RefreshOrchestrator { get; }
public UpdateNotifier UpdateNotifier { get; }
public event Action? OnSnapshotsChanged;
```

In the ctor, after `Scheduler = ...`:

```csharp
RefreshOrchestrator = new RefreshOrchestrator(
    Scheduler,
    () => TimeSpan.FromMinutes(Math.Clamp(Settings.RefreshIntervalMinutes, 1, 60)),
    shutdownToken: default);  // wire real CTS in Step 4.6
RefreshOrchestrator.Refreshed += (_, _) => OnSnapshotsChanged?.Invoke();

UpdateNotifier = new UpdateNotifier(
    new GitHubUpdateChecker(HttpClient, AppVersionInfoFromCodexBarCore.Current),
    result => { /* fire AppNotification in Task 8 */ },
    shutdownToken: default);
```

(`AppVersionInfo` will need to move to Core too if it isn't already — same `git mv` treatment.)

- [ ] **Step 4.6: Start them in App.xaml.cs OnLaunched**

After the existing one-shot refresh in `App.xaml.cs:OnLaunched`:

```csharp
shell.RefreshOrchestrator.Start();
if (shell.Settings.CheckForUpdatesAutomatically)
{
    shell.UpdateNotifier.Start(TimeSpan.FromHours(24));
    _ = shell.UpdateNotifier.CheckNowAsync(default);
}
```

And dispose on app exit — wire `Application.Current.Exiting += (_, _) => shell.Dispose();` or use the OnQuit callback chain.

- [ ] **Step 4.7: Build and verify**

Launch. Leave open for a minute. Confirm tray icon badge updates and popover content refreshes without intervention.

- [ ] **Step 4.8: Commit**

```bash
git commit -am "Port RefreshOrchestrator + UpdateNotifier to CodexBar.WinUI"
```

---

### Task 5: Single-instance enforcement

**Files:**
- Create: `src/windows/CodexBar.WinUI/Services/SingleInstance.cs`
- Modify: `src/windows/CodexBar.WinUI/Program.cs` (new file — required for early instance check before XAML loads)
- Modify: `src/windows/CodexBar.WinUI/CodexBar.WinUI.csproj` — add `<DisableRuntimeMarshalling>true</DisableRuntimeMarshalling>`

WinUI 3 needs a custom `Main` to do single-instance detection before `Application.Start` is called. The default entry point is generated; we replace it.

- [ ] **Step 5.1: Author Program.cs**

Create `src/windows/CodexBar.WinUI/Program.cs`:

```csharp
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;

namespace CodexBar.WinUI;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        var isRedirect = DecideRedirection();
        if (!isRedirect)
        {
            Application.Start(_ =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
        return 0;
    }

    private static bool DecideRedirection()
    {
        var keyInstance = AppInstance.FindOrRegisterForKey("CodexBar.WinUI.SingleInstance");
        if (!keyInstance.IsCurrent)
        {
            var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            keyInstance.RedirectActivationToAsync(activatedArgs).AsTask().GetAwaiter().GetResult();
            return true;
        }
        return false;
    }
}
```

- [ ] **Step 5.2: Disable auto-generated Main**

Edit `src/windows/CodexBar.WinUI/CodexBar.WinUI.csproj`. Add to the existing `<PropertyGroup>`:

```xml
<DefineConstants>$(DefineConstants);DISABLE_XAML_GENERATED_MAIN</DefineConstants>
```

- [ ] **Step 5.3: Build and verify**

Build. Launch the .exe. While running, launch it again. Expected: the second instance immediately exits; the first remains the sole running process.

- [ ] **Step 5.4: Test (where reasonable)**

Single-instance is hard to test in-process because it requires actually launching two processes. Skip unit testing for this — capture as a manual test instruction.

- [ ] **Step 5.5: Commit**

```bash
git commit -am "Add single-instance enforcement to CodexBar.WinUI"
```

---

### Task 6: Right-click MenuFlyout (replace WinForms ContextMenu)

**Files:**
- Modify: `src/windows/CodexBar.WinUI/Services/TrayHost.cs` — H.NotifyIcon.WinUI supports `ContextFlyout` directly; assign a `MenuFlyout` instead of going through WinForms.
- Modify: `src/windows/CodexBar.WinUI/App.xaml.cs` — wire menu items to the same callbacks as before.

H.NotifyIcon.WinUI's `TaskbarIcon.ContextFlyout` accepts a `Microsoft.UI.Xaml.Controls.MenuFlyout`. This gives us a native WinUI 3 context menu (Fluent style, accent-respecting) instead of the WinForms ContextMenuStrip we've been carrying.

- [ ] **Step 6.1: Construct MenuFlyout in TrayHost**

In `src/windows/CodexBar.WinUI/Services/TrayHost.cs`, add to the ctor:

```csharp
public Action? OnSettingsClick { get; set; }
public Action? OnAboutClick { get; set; }
public Action? OnQuitClick { get; set; }

public TrayHost()
{
    icon.NoLeftClickDelay = true;
    icon.ToolTipText = "CodexBar";
    icon.LeftClickCommand = new RelayCommand(() => LeftClick?.Invoke(this, EventArgs.Empty));

    var menu = new MenuFlyout();
    var settings = new MenuFlyoutItem { Text = "Settings..." };
    settings.Click += (_, _) => OnSettingsClick?.Invoke();
    menu.Items.Add(settings);

    var about = new MenuFlyoutItem { Text = "About CodexBar" };
    about.Click += (_, _) => OnAboutClick?.Invoke();
    menu.Items.Add(about);

    menu.Items.Add(new MenuFlyoutSeparator());

    var quit = new MenuFlyoutItem { Text = "Quit" };
    quit.Click += (_, _) => OnQuitClick?.Invoke();
    menu.Items.Add(quit);

    icon.ContextFlyout = menu;
}
```

Add `using Microsoft.UI.Xaml.Controls;` at top.

- [ ] **Step 6.2: Remove WinForms callback path**

In `App.xaml.cs`, replace the old TrayIconHost ctor call (which took 4 callbacks) with property assignments after `new TrayHost()`:

```csharp
tray = new TrayHost();
tray.LeftClick += (_, _) => uiDispatcher.TryEnqueue(TogglePopover);
tray.OnSettingsClick = () => uiDispatcher.TryEnqueue(ShowSettings);
tray.OnAboutClick = () => uiDispatcher.TryEnqueue(ShowAbout);
tray.OnQuitClick = () => uiDispatcher.TryEnqueue(() => Application.Current.Exit());
tray.Show();
```

`ShowSettings` and `ShowAbout` are no-ops for now — Task 7 and Task 9 implement them.

- [ ] **Step 6.3: Build and verify**

Launch. Right-click tray. Expected: Fluent-styled context menu with Settings / About / Quit. Clicking each invokes its callback (currently no-ops for Settings/About, exit for Quit).

- [ ] **Step 6.4: Commit**

```bash
git commit -am "Use WinUI MenuFlyout for tray context menu"
```

---

### Task 7: Settings window (NavigationView + SettingsCard)

This is the largest task. It rebuilds the Settings UI in WinUI 3 idiom.

**Files:**
- Create: `src/windows/CodexBar.WinUI/Views/SettingsWindow.xaml`
- Create: `src/windows/CodexBar.WinUI/Views/SettingsWindow.xaml.cs`
- Create: `src/windows/CodexBar.WinUI/ViewModels/SettingsViewModel.cs`
- Create: `src/windows/CodexBar.Tests/WinUiSettingsViewModelTests.cs`
- Modify: `src/windows/CodexBar.WinUI/CodexBar.WinUI.csproj` — add `CommunityToolkit.WinUI.Controls.SettingsControls`
- Modify: `src/windows/CodexBar.WinUI/App.xaml.cs` — `ShowSettings()` method opens this window

Reference for behavior: `src/windows/CodexBar.WinApp/Views/SettingsWindow.xaml(.cs)` and `src/windows/CodexBar.WinApp/ViewModels/SettingsViewModel.cs`. Port the BEHAVIOR (provider toggles, refresh interval slider, dock toggle, launch-at-startup toggle, "Check for updates automatically" toggle, manual cookie inputs for Cursor + Claude, "Test connection" button per provider, "Check for Updates" button) but rewrite the LAYOUT using Fluent `SettingsCard`.

- [ ] **Step 7.1: Add SettingsControls package**

In csproj:

```xml
<PackageReference Include="CommunityToolkit.WinUI.Controls.SettingsControls" Version="8.1.240916" />
```

- [ ] **Step 7.2: Port SettingsViewModel**

Read `src/windows/CodexBar.WinApp/ViewModels/SettingsViewModel.cs` for the existing logic. Create `src/windows/CodexBar.WinUI/ViewModels/SettingsViewModel.cs` using `CommunityToolkit.Mvvm` source generators. Properties to expose:

- `bool CodexEnabled`, `bool ClaudeEnabled`, `bool CursorEnabled`, `bool GeminiEnabled`
- `int RefreshIntervalMinutes` (range 1–60)
- `bool DockOverviewNearTaskbar`
- `bool LaunchAtStartup`
- `bool CheckForUpdatesAutomatically`
- `bool ShowUsageAsUsed`
- `string ClaudeManualCookieHeader` / `string CursorManualCookieHeader`
- `string UpdateStatusText` (computed from `LatestUpdateCheck`)
- Commands: `TestCodex`, `TestClaude`, `TestCursor`, `TestGemini`, `CheckForUpdates`, `Save`, `OpenBugReport`

Skeleton:

```csharp
using CodexBar.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodexBar.WinUI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private bool codexEnabled;
    [ObservableProperty] private bool claudeEnabled;
    [ObservableProperty] private bool cursorEnabled;
    [ObservableProperty] private bool geminiEnabled;
    [ObservableProperty] private int refreshIntervalMinutes;
    [ObservableProperty] private bool dockOverviewNearTaskbar;
    [ObservableProperty] private bool launchAtStartup;
    [ObservableProperty] private bool checkForUpdatesAutomatically;
    [ObservableProperty] private bool showUsageAsUsed;
    [ObservableProperty] private string claudeManualCookieHeader = string.Empty;
    [ObservableProperty] private string cursorManualCookieHeader = string.Empty;
    [ObservableProperty] private string updateStatusText = string.Empty;

    public SettingsViewModel(AppSettings settings)
    {
        codexEnabled = settings.CodexEnabled;
        claudeEnabled = settings.ClaudeEnabled;
        cursorEnabled = settings.CursorEnabled;
        geminiEnabled = settings.GeminiEnabled;
        refreshIntervalMinutes = settings.RefreshIntervalMinutes;
        dockOverviewNearTaskbar = settings.DockOverviewNearTaskbar;
        launchAtStartup = settings.LaunchAtStartup;
        checkForUpdatesAutomatically = settings.CheckForUpdatesAutomatically;
        showUsageAsUsed = settings.ShowUsageAsUsed;
        claudeManualCookieHeader = settings.ClaudeManualCookieHeader ?? string.Empty;
        cursorManualCookieHeader = settings.CursorManualCookieHeader ?? string.Empty;
    }

    public AppSettings ToSettings() => new()
    {
        CodexEnabled = CodexEnabled,
        ClaudeEnabled = ClaudeEnabled,
        CursorEnabled = CursorEnabled,
        GeminiEnabled = GeminiEnabled,
        RefreshIntervalMinutes = RefreshIntervalMinutes,
        DockOverviewNearTaskbar = DockOverviewNearTaskbar,
        LaunchAtStartup = LaunchAtStartup,
        CheckForUpdatesAutomatically = CheckForUpdatesAutomatically,
        ShowUsageAsUsed = ShowUsageAsUsed,
        ClaudeManualCookieHeader = string.IsNullOrWhiteSpace(ClaudeManualCookieHeader) ? null : ClaudeManualCookieHeader,
        CursorManualCookieHeader = string.IsNullOrWhiteSpace(CursorManualCookieHeader) ? null : CursorManualCookieHeader,
    };
}
```

(Save/test/check-update commands wired in Step 7.4 — they call into AppShell.)

- [ ] **Step 7.3: Write SettingsWindow.xaml**

Create `src/windows/CodexBar.WinUI/Views/SettingsWindow.xaml`:

```xml
<Window
    x:Class="CodexBar.WinUI.Views.SettingsWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="using:Microsoft.UI.Xaml.Controls"
    xmlns:ctk="using:CommunityToolkit.WinUI.Controls">
    <Grid Padding="24" RowSpacing="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Style="{StaticResource TitleTextBlockStyle}" Text="Settings" />

        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
            <StackPanel Spacing="8">
                <controls:Expander Header="Providers" IsExpanded="True">
                    <StackPanel Spacing="4">
                        <ctk:SettingsCard Header="Codex">
                            <ToggleSwitch IsOn="{x:Bind ViewModel.CodexEnabled, Mode=TwoWay}" />
                        </ctk:SettingsCard>
                        <ctk:SettingsCard Header="Claude">
                            <ToggleSwitch IsOn="{x:Bind ViewModel.ClaudeEnabled, Mode=TwoWay}" />
                        </ctk:SettingsCard>
                        <ctk:SettingsCard Header="Cursor">
                            <ToggleSwitch IsOn="{x:Bind ViewModel.CursorEnabled, Mode=TwoWay}" />
                        </ctk:SettingsCard>
                        <ctk:SettingsCard Header="Gemini">
                            <ToggleSwitch IsOn="{x:Bind ViewModel.GeminiEnabled, Mode=TwoWay}" />
                        </ctk:SettingsCard>
                    </StackPanel>
                </controls:Expander>

                <controls:Expander Header="Refresh" IsExpanded="True">
                    <ctk:SettingsCard Header="Refresh interval" Description="How often CodexBar queries each provider.">
                        <NumberBox Value="{x:Bind ViewModel.RefreshIntervalMinutes, Mode=TwoWay}"
                                   Minimum="1" Maximum="60" Width="100" />
                    </ctk:SettingsCard>
                </controls:Expander>

                <controls:Expander Header="Display" IsExpanded="True">
                    <StackPanel Spacing="4">
                        <ctk:SettingsCard Header="Show taskbar dock" Description="Display a compact overview pinned near the taskbar.">
                            <ToggleSwitch IsOn="{x:Bind ViewModel.DockOverviewNearTaskbar, Mode=TwoWay}" />
                        </ctk:SettingsCard>
                        <ctk:SettingsCard Header="Show usage as used" Description="When off, shows remaining instead of used.">
                            <ToggleSwitch IsOn="{x:Bind ViewModel.ShowUsageAsUsed, Mode=TwoWay}" />
                        </ctk:SettingsCard>
                    </StackPanel>
                </controls:Expander>

                <controls:Expander Header="System" IsExpanded="True">
                    <StackPanel Spacing="4">
                        <ctk:SettingsCard Header="Launch at startup">
                            <ToggleSwitch IsOn="{x:Bind ViewModel.LaunchAtStartup, Mode=TwoWay}" />
                        </ctk:SettingsCard>
                        <ctk:SettingsCard Header="Check for updates automatically" Description="Compare your build with the latest GitHub release every 24 hours.">
                            <ToggleSwitch IsOn="{x:Bind ViewModel.CheckForUpdatesAutomatically, Mode=TwoWay}" />
                        </ctk:SettingsCard>
                    </StackPanel>
                </controls:Expander>

                <controls:Expander Header="Advanced credentials">
                    <StackPanel Spacing="8">
                        <ctk:SettingsCard Header="Cursor manual cookie" Description="Cookie: header copied from a signed-in Cursor browser request.">
                            <TextBox Text="{x:Bind ViewModel.CursorManualCookieHeader, Mode=TwoWay}" Width="300" />
                        </ctk:SettingsCard>
                        <ctk:SettingsCard Header="Claude manual cookie" Description="Fallback when OAuth credentials aren't available.">
                            <TextBox Text="{x:Bind ViewModel.ClaudeManualCookieHeader, Mode=TwoWay}" Width="300" />
                        </ctk:SettingsCard>
                    </StackPanel>
                </controls:Expander>
            </StackPanel>
        </ScrollViewer>

        <StackPanel Grid.Row="2" Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <Button Content="Cancel" Click="Cancel_Click" />
            <Button Content="Save" Style="{StaticResource AccentButtonStyle}" Click="Save_Click" />
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 7.4: Code-behind**

`src/windows/CodexBar.WinUI/Views/SettingsWindow.xaml.cs`:

```csharp
using System;
using CodexBar.Core.Settings;
using CodexBar.WinUI.ViewModels;
using Microsoft.UI.Xaml;

namespace CodexBar.WinUI.Views;

public sealed partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }
    private readonly Action<AppSettings> onSave;

    public SettingsWindow(SettingsViewModel viewModel, Action<AppSettings> onSave)
    {
        ViewModel = viewModel;
        this.onSave = onSave;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(540, 720));
        Title = "CodexBar Settings";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        onSave(ViewModel.ToSettings());
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
```

- [ ] **Step 7.5: Wire ShowSettings in App.xaml.cs**

Add an `App.xaml.cs:ShowSettings` method:

```csharp
private SettingsWindow? settingsWindow;

private void ShowSettings()
{
    if (settingsWindow is not null) { settingsWindow.Activate(); return; }
    if (shell is null) return;

    var vm = new SettingsViewModel(shell.Settings);
    settingsWindow = new SettingsWindow(vm, settings =>
    {
        _ = shell.ApplySettingsAsync(settings);
    });
    settingsWindow.Closed += (_, _) => settingsWindow = null;
    settingsWindow.Activate();
}
```

`shell.ApplySettingsAsync(settings)` will be defined in Task 8.

- [ ] **Step 7.6: Tests**

Create `src/windows/CodexBar.Tests/WinUiSettingsViewModelTests.cs`. Test:
1. Round-trip — construct VM from settings, call `ToSettings()`, fields equal.
2. Whitespace-only cookie headers become `null` in ToSettings.
3. RefreshIntervalMinutes is preserved.

- [ ] **Step 7.7: Build + verify**

Launch app, right-click → Settings. Expected: Fluent SettingsCard-styled window. Toggle a provider, hit Save, popover re-renders without that provider.

- [ ] **Step 7.8: Commit**

```bash
git commit -am "Add Settings window with Fluent SettingsCard layout"
```

---

### Task 8: Port ApplySettings to AppShell

**Files:**
- Modify: `src/windows/CodexBar.WinUI/AppHostBuilder.cs` (or wherever `AppShell` lives) — add `ApplySettingsAsync(AppSettings)`
- Modify: `src/windows/CodexBar.Core/Refresh/SnapshotStore.cs` — already has `Remove(UsageProvider)` from Phase 1 ultrareview fix

Mirror the WPF `AppShellController.ApplySettings` logic: call `services.ReconfigureProviders(settings)`, restart `RefreshOrchestrator`, restart `UpdateNotifier` with optional immediate check, remove snapshots for disabled providers, persist settings via `JsonSettingsStore`, fire `OnSnapshotsChanged`.

- [ ] **Step 8.1: Move AppServices to Core (or duplicate inline)**

Phase 1's `AppServices` lives in `CodexBar.WinApp` but has zero WPF dependencies. To share it with WinUI, move it:

```bash
git mv src/windows/CodexBar.WinApp/AppServices.cs src/windows/CodexBar.Core/AppServices.cs
```

Edit namespace: `namespace CodexBar.WinApp;` → `namespace CodexBar.Core;`. Grep callers, fix usings (WPF `AppShellController` and tests will need updating).

- [ ] **Step 8.2: Add ApplySettingsAsync to AppShell**

In `src/windows/CodexBar.WinUI/AppHostBuilder.cs`:

```csharp
public async Task ApplySettingsAsync(AppSettings settings)
{
    var previousSnapshots = Store.All();

    RefreshOrchestrator.Stop();

    Services.ReconfigureProviders(settings);

    foreach (var provider in System.Enum.GetValues<UsageProvider>())
    {
        if (!IsEnabled(settings, provider))
        {
            Store.Remove(provider);
        }
    }

    UpdateNotifier.Stop();
    if (settings.CheckForUpdatesAutomatically)
    {
        UpdateNotifier.Start(TimeSpan.FromHours(24));
        _ = UpdateNotifier.CheckNowAsync(default);
    }

    var settingsStore = new JsonSettingsStore(Paths.SettingsFile);
    await settingsStore.SaveAsync(settings, default);

    RefreshOrchestrator.Start();
    await RefreshOrchestrator.RefreshNowAsync(default);
    OnSnapshotsChanged?.Invoke();
}

private static bool IsEnabled(AppSettings s, UsageProvider p) => p switch
{
    UsageProvider.Codex => s.CodexEnabled,
    UsageProvider.Claude => s.ClaudeEnabled,
    UsageProvider.Cursor => s.CursorEnabled,
    UsageProvider.Gemini => s.GeminiEnabled,
    _ => true,
};
```

- [ ] **Step 8.3: Build + verify**

Launch, change a setting (e.g., disable Claude), Save. Expected: popover reflects the change immediately, Claude tab gone. Re-enable, Save. Popover regains Claude tab with "Refreshing..." then real data on next refresh.

- [ ] **Step 8.4: Commit**

```bash
git commit -am "Port ApplySettings flow to CodexBar.WinUI AppShell"
```

---

### Task 9: About window

**Files:**
- Create: `src/windows/CodexBar.WinUI/ViewModels/AboutViewModel.cs`
- Create: `src/windows/CodexBar.WinUI/Views/AboutWindow.xaml(.cs)`
- Modify: `src/windows/CodexBar.WinUI/App.xaml.cs` — `ShowAbout()` opens the window

- [ ] **Step 9.1: ViewModel**

```csharp
using CodexBar.Core;  // assuming AppVersionInfo lives here after Task 4 sub-step

namespace CodexBar.WinUI.ViewModels;

public sealed class AboutViewModel
{
    public AboutViewModel(AppVersionInfo version)
    {
        DisplayVersion = version.CurrentTag;
        Channel = version.Channel;
    }
    public string DisplayVersion { get; }
    public string Channel { get; }
}
```

- [ ] **Step 9.2: XAML**

`src/windows/CodexBar.WinUI/Views/AboutWindow.xaml`:

```xml
<Window
    x:Class="CodexBar.WinUI.Views.AboutWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="About CodexBar">
    <StackPanel Padding="24" Spacing="8">
        <TextBlock Style="{StaticResource TitleTextBlockStyle}" Text="CodexBar" />
        <TextBlock Style="{StaticResource BodyTextBlockStyle}" Text="CodexBar for Windows" Opacity="0.8" />
        <TextBlock Style="{StaticResource CaptionTextBlockStyle}" Opacity="0.7">
            <Run Text="Version " /><Run Text="{x:Bind ViewModel.DisplayVersion}" />
        </TextBlock>
        <TextBlock Style="{StaticResource CaptionTextBlockStyle}" Opacity="0.7">
            <Run Text="Release channel: " /><Run Text="{x:Bind ViewModel.Channel}" />
        </TextBlock>
        <TextBlock Style="{StaticResource CaptionTextBlockStyle}" Opacity="0.7"
                   Text="Inspired by Peter Steinberger's CodexBar" TextWrapping="Wrap" />
        <HyperlinkButton Content="https://github.com/steipete/CodexBar" NavigateUri="https://github.com/steipete/CodexBar" />
        <Button Content="OK" HorizontalAlignment="Right" Click="Ok_Click" Margin="0,12,0,0" />
    </StackPanel>
</Window>
```

- [ ] **Step 9.3: Code-behind**

```csharp
using Microsoft.UI.Xaml;
using CodexBar.WinUI.ViewModels;

namespace CodexBar.WinUI.Views;

public sealed partial class AboutWindow : Window
{
    public AboutViewModel ViewModel { get; }
    public AboutWindow(AboutViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(380, 320));
    }
    private void Ok_Click(object sender, RoutedEventArgs e) => Close();
}
```

- [ ] **Step 9.4: Wire ShowAbout**

In `App.xaml.cs`:

```csharp
private AboutWindow? aboutWindow;
private void ShowAbout()
{
    if (aboutWindow is not null) { aboutWindow.Activate(); return; }
    aboutWindow = new AboutWindow(new AboutViewModel(AppVersionInfo.Current));
    aboutWindow.Closed += (_, _) => aboutWindow = null;
    aboutWindow.Activate();
}
```

- [ ] **Step 9.5: Commit**

```bash
git commit -am "Add WinUI About window"
```

---

### Task 10: First-run onboarding

**Files:**
- Create: `src/windows/CodexBar.WinUI/ViewModels/FirstRunViewModel.cs`
- Create: `src/windows/CodexBar.WinUI/Views/FirstRunWindow.xaml(.cs)`
- Modify: `src/windows/CodexBar.WinUI/App.xaml.cs` — show first-run window if no settings file existed at launch

Port from `src/windows/CodexBar.WinApp/Views/FirstRunWindow.xaml(.cs)` + `FirstRunViewModel.cs`. Rebuild XAML in WinUI 3 idiom — provider checkboxes + Help buttons + Get Started / Skip buttons.

- [ ] **Step 10.1-10.5:** Same pattern as Task 7 (Settings) but smaller. Port the ViewModel, build the XAML using `CheckBox` + `HyperlinkButton` for Help, wire `GetStarted` to save the user's chosen providers and dismiss.

- [ ] **Step 10.6: Trigger detection**

In `App.xaml.cs:OnLaunched`, after `AppHostBuilder.BuildAsync()`:

```csharp
if (!File.Exists(shell.Paths.SettingsFile))
{
    ShowFirstRun();
}
```

- [ ] **Step 10.7: Commit**

```bash
git commit -am "Add WinUI first-run onboarding window"
```

---

### Task 11: Taskbar dock window

**Files:**
- Create: `src/windows/CodexBar.WinUI/ViewModels/TaskbarDockViewModel.cs`
- Create: `src/windows/CodexBar.WinUI/Views/TaskbarDockWindow.xaml(.cs)`
- Modify: `src/windows/CodexBar.WinUI/App.xaml.cs` — show/hide based on `shell.Settings.DockOverviewNearTaskbar`; reposition on work-area change

Port from `src/windows/CodexBar.WinApp/Views/TaskbarDockWindow.xaml(.cs)` + `TaskbarDockViewModel.cs`. Use the same `WindowCoordinator.CalculateTaskbarDockPosition` math — that helper already lives in `CodexBar.WinApp/Services/WindowCoordinator.Positioning.cs`. For WinUI, move that math (alongside `CalculatePopoverPosition` etc.) into `CodexBar.Core/WinUI/PopoverPositioner.cs` from Phase 2 or into a new `CodexBar.Core/WinUI/DockPositioner.cs`.

- [ ] **Step 11.1: Move dock positioning math to Core**

In `src/windows/CodexBar.Core/WinUI/PopoverPositioner.cs`, add:

```csharp
public static (int Left, int Top) CalculateTaskbarDock(int width, int height, int workAreaX, int workAreaY, int workAreaWidth, int workAreaHeight)
{
    // Anchor: bottom-right of the work area, with margin.
    const int margin = 8;
    return (workAreaX + workAreaWidth - width - margin, workAreaY + workAreaHeight - height - margin);
}
```

(Adjust to match `WindowCoordinator.CalculateTaskbarDockPosition` exactly.)

- [ ] **Step 11.2: Port TaskbarDockViewModel**

Mirror the WPF version. It exposes `HasTiles` (bool) and a list of `ProviderTileViewModel` (provider name + percent + color).

- [ ] **Step 11.3: TaskbarDockWindow.xaml**

Compact horizontal strip of provider tiles. Acrylic backdrop. ~440×80 px.

- [ ] **Step 11.4: Show/hide in App.xaml.cs**

```csharp
private TaskbarDockWindow? dock;
private void UpdateTaskbarDock()
{
    var enabled = shell?.Settings.DockOverviewNearTaskbar ?? false;
    if (!enabled)
    {
        dock?.Close();
        dock = null;
        return;
    }
    if (dock is null && shell is not null)
    {
        var vm = new TaskbarDockViewModel(shell.Store.All(), shell.Settings.ShowUsageAsUsed);
        if (!vm.HasTiles) return;
        dock = new TaskbarDockWindow(vm);
        dock.Closed += (_, _) => dock = null;
        PositionDock(dock);
        dock.Activate();
    }
    else if (dock is not null)
    {
        dock.DataContext = new TaskbarDockViewModel(shell.Store.All(), shell.Settings.ShowUsageAsUsed);
    }
}
```

Subscribe to `shell.OnSnapshotsChanged` to call `UpdateTaskbarDock`.

- [ ] **Step 11.5: Commit**

```bash
git commit -am "Add WinUI taskbar dock window"
```

---

### Task 12: Popover footer + commands

**Files:**
- Modify: `src/windows/CodexBar.WinUI/ViewModels/PopoverViewModel.cs` — add 6 `[RelayCommand]` properties (AddAccount, UsageDashboard, StatusPage, Settings, About, Quit)
- Modify: `src/windows/CodexBar.WinUI/Views/PopoverWindow.xaml` — add footer row with 6 buttons bound to those commands
- Modify: `src/windows/CodexBar.WinUI/App.xaml.cs:TogglePopover` — pass the 6 callbacks to the VM

- [ ] **Step 12.1: VM commands**

```csharp
[RelayCommand] private void AddAccount() => addAccount?.Invoke();
[RelayCommand] private void UsageDashboard() => usageDashboard?.Invoke();
[RelayCommand] private void StatusPage() => statusPage?.Invoke();
[RelayCommand] private void Settings() => settings?.Invoke();
[RelayCommand] private void About() => about?.Invoke();
[RelayCommand] private void Quit() => quit?.Invoke();
```

Constructor gains 6 `Action?` parameters with defaults of `null`.

- [ ] **Step 12.2: XAML footer**

In `PopoverWindow.xaml`, add at the bottom (above the LiveIndicatorText row):

```xml
<Grid Grid.Row="?">  <!-- adjust row -->
    <StackPanel Orientation="Horizontal" Spacing="4" HorizontalAlignment="Right">
        <Button Style="{StaticResource SubtleButtonStyle}" Command="{x:Bind ViewModel.SettingsCommand}" ToolTipService.ToolTip="Settings">
            <FontIcon Glyph="&#xE713;" />
        </Button>
        <Button Style="{StaticResource SubtleButtonStyle}" Command="{x:Bind ViewModel.AboutCommand}" ToolTipService.ToolTip="About">
            <FontIcon Glyph="&#xE946;" />
        </Button>
        <Button Style="{StaticResource SubtleButtonStyle}" Command="{x:Bind ViewModel.QuitCommand}" ToolTipService.ToolTip="Quit">
            <FontIcon Glyph="&#xE8BB;" />
        </Button>
    </StackPanel>
</Grid>
```

(Add the other three — Add Account, Dashboard, Status Page — similarly, on the left side.)

- [ ] **Step 12.3: App.xaml.cs wiring**

In `TogglePopover`, pass the callbacks when constructing the VM:

```csharp
var vm = new PopoverViewModel(
    shell.Store.All(),
    UsageProvider.Codex,
    shell.Settings.ShowUsageAsUsed,
    refreshStates: shell.RefreshStates,
    settings: () => uiDispatcher.TryEnqueue(ShowSettings),
    about: () => uiDispatcher.TryEnqueue(ShowAbout),
    quit: () => uiDispatcher.TryEnqueue(() => Application.Current.Exit()),
    usageDashboard: () => OpenUri(ProviderLinks.DashboardUri(activeProvider)),
    statusPage: () => OpenUri(ProviderLinks.StatusUri(activeProvider)),
    addAccount: () => uiDispatcher.TryEnqueue(ShowSettings));
```

`ProviderLinks` lives in `CodexBar.WinApp` today — move it to `CodexBar.Core/Providers/ProviderLinks.cs`.

- [ ] **Step 12.4: Commit**

```bash
git commit -am "Add popover footer commands wired to AppShell"
```

---

### Task 13: Update notifications via Windows AppNotification

**Files:**
- Modify: `src/windows/CodexBar.WinUI/CodexBar.WinUI.csproj` — add reference to `Microsoft.Windows.AppNotifications`
- Modify: `src/windows/CodexBar.WinUI/Services/AppShell.cs` — when `UpdateNotifier` callback fires, post an AppNotification

- [ ] **Step 13.1: Register the notification manager in Program.cs**

```csharp
Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Register();
```

(In `Program.Main` before `Application.Start`.)

- [ ] **Step 13.2: Post a notification when an update is found**

In the `UpdateNotifier` callback wiring (Task 4):

```csharp
result => uiDispatcher.TryEnqueue(() =>
{
    var builder = new Microsoft.Windows.AppNotifications.Builder.AppNotificationBuilder()
        .AddText("CodexBar update available")
        .AddText($"{result.LatestTag} is available.")
        .AddButton(new Microsoft.Windows.AppNotifications.Builder.AppNotificationButton("Open release")
            .AddArgument("action", "open-release")
            .AddArgument("url", result.ReleaseUrl.ToString()));
    Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Show(builder.BuildNotification());
})
```

Handle the activation in `App.OnLaunched` by checking `AppInstance.GetCurrent().GetActivatedEventArgs()` — if it's a notification activation, parse the argument and open the URL.

- [ ] **Step 13.3: Commit**

```bash
git commit -am "Surface update available via Windows AppNotification"
```

---

### Task 14: Bug report flow

**Files:**
- Modify: `src/windows/CodexBar.WinUI/Views/SettingsWindow.xaml` — add "Report a Bug..." button
- Modify: `src/windows/CodexBar.WinUI/ViewModels/SettingsViewModel.cs` — add `OpenBugReport` command
- Modify: `src/windows/CodexBar.WinApp/BugReportBuilder.cs` — move to `src/windows/CodexBar.Core/BugReportBuilder.cs` so WinUI can reuse it

- [ ] **Step 14.1: Move BugReportBuilder to Core**

```bash
git mv src/windows/CodexBar.WinApp/BugReportBuilder.cs src/windows/CodexBar.Core/BugReportBuilder.cs
```

Update namespace and callers.

- [ ] **Step 14.2: Wire button**

```csharp
[RelayCommand]
private void OpenBugReport()
{
    var summary = BugReportBuilder.Build(/* version, settings snapshot, last error */);
    System.Windows.Clipboard.SetText(summary);  // ← WPF clipboard won't work in WinUI; use DataPackage instead
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = "https://github.com/dontcallmejames/CodexBar-Windows/issues/new",
        UseShellExecute = true,
    });
}
```

Replace the WPF Clipboard call with WinUI clipboard:

```csharp
var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
pkg.SetText(summary);
Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
```

- [ ] **Step 14.3: Commit**

```bash
git commit -am "Add bug-report flow to WinUI Settings"
```

---

### Task 15: Cutover — WinUI is the primary shell

This is the irreversible task. After this, the installer ships the WinUI build and the WPF shell is archived.

**Files:**
- Modify: `Scripts/package-windows.ps1` — point at `CodexBar.WinUI` instead of `CodexBar.WinApp`
- Modify: `Scripts/package-windows-installer.ps1` — same
- Modify: `installer/windows/CodexBarSetup.iss` — update source path to WinUI build output
- Modify: `.github/workflows/windows.yml` — build CodexBar.WinUI
- Modify: `AGENTS.md`, `README.md` — update build instructions and shell references
- Move: `src/windows/CodexBar.WinApp/**` → `legacy-wpf/CodexBar.WinApp/**` (mirror the `legacy-macos/` archival pattern)
- Move: `src/windows/CodexBar.Tray/**` → `legacy-wpf/CodexBar.Tray/**` if the WinUI app no longer uses it
- Modify: `src/windows/CodexBar.Windows.sln` — remove WPF projects from the solution
- Modify: `src/windows/CodexBar.Tests/CodexBar.Tests.csproj` — remove the WPF-specific tests (or move them to a `legacy-wpf/CodexBar.Tests/` archive)

- [ ] **Step 15.1: Switch installer scripts**

In `Scripts/package-windows.ps1` and `package-windows-installer.ps1`, change every reference to `CodexBar.WinApp` paths to `CodexBar.WinUI` paths. Update output filenames if needed.

- [ ] **Step 15.2: Update CI**

In `.github/workflows/windows.yml`, change the publish target from `CodexBar.WinApp.csproj` to `CodexBar.WinUI.csproj`. The build command stays largely the same; the artifact paths change.

- [ ] **Step 15.3: Move WPF to legacy-wpf/**

```bash
mkdir legacy-wpf
git mv src/windows/CodexBar.WinApp legacy-wpf/CodexBar.WinApp
git mv src/windows/CodexBar.Tray legacy-wpf/CodexBar.Tray
```

Remove the WPF projects from `src/windows/CodexBar.Windows.sln`:

```bash
C:\tmp\dotnet\dotnet.exe sln src/windows/CodexBar.Windows.sln remove legacy-wpf/CodexBar.WinApp/CodexBar.WinApp.csproj
C:\tmp\dotnet\dotnet.exe sln src/windows/CodexBar.Windows.sln remove legacy-wpf/CodexBar.Tray/CodexBar.Tray.csproj
```

- [ ] **Step 15.4: Move or delete WPF-specific tests**

Tests that exercise `WindowCoordinator`, `App.xaml.cs` shell behavior, `PopoverViewModel` (WPF), `SettingsWindow` (WPF), etc. are now obsolete. Either:
- Port the meaningful ones to WinUI-equivalent tests, OR
- Move them to `legacy-wpf/CodexBar.Tests/` and exclude from the main solution.

For each test file in `src/windows/CodexBar.Tests/`, decide:
- Tests on `CodexBar.Core` (RefreshScheduler, AdaptiveBackoff, ProviderRefreshState, all provider tests, snapshot store, settings store, packaging script tests, public release doc tests, MeterIconRenderer if any): keep.
- Tests on WPF shell (`WpfShellTests`, `FirstRunOnboardingTests` if it exercises WPF UI, etc.): port to WinUI equivalents or archive.

- [ ] **Step 15.5: Update README + AGENTS**

`README.md`: change `Use the Windows solution under src/windows.` references that mention `CodexBar.WinApp` to `CodexBar.WinUI`. Document that the app requires Windows App Runtime (or that the build is self-contained). Update the legacy-macos section to mention legacy-wpf alongside.

`AGENTS.md`: update build/test commands. Note that the WPF shell is archived under `legacy-wpf/`. Update the test command to reflect the new test scope.

- [ ] **Step 15.6: Final build + test + smoke**

```
C:\tmp\dotnet\dotnet.exe build src\windows\CodexBar.Windows.sln -c Release
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln -c Release --verbosity minimal
.\Scripts\package-windows-installer.ps1
```

Install the produced installer on a clean Win11 machine. Verify the full user flow:
- App appears in Start menu under "CodexBar"
- Launch → tray icon appears
- Left-click → Acrylic popover, real provider data
- Right-click → Settings / About / Quit
- Settings save → behavior updates immediately
- About shows version
- First-run shows on clean install
- Update check fires
- Quit cleanly

- [ ] **Step 15.7: Commit**

```bash
git commit -am "Cutover: WinUI 3 is the primary shell; WPF archived to legacy-wpf/"
```

- [ ] **Step 15.8: PR + merge + tag a preview release**

Push the branch. Open PR. Squash or merge-commit. Tag a preview release (`v0.27-preview.1` or similar) so the new build is downloadable.

---

## Self-Review Checklist (run after writing implementation)

- [ ] WPF shell deleted from solution (now under `legacy-wpf/`).
- [ ] No `Microsoft.Win32` (WPF), `System.Windows.Threading.DispatcherTimer`, or `System.Windows.Forms` references remain in any active project.
- [ ] All Phase 1 features still work: per-provider backoff visible (try rate-limiting Claude), live indicator ticks, settings persistence, update check, tray icon updates on refresh.
- [ ] All Phase 2 features still work: Acrylic backdrop, live theme/accent reactivity.
- [ ] Tests: no test count regression except for legitimately archived WPF UI tests.
- [ ] Manual test plan in README updated.
- [ ] Installer artifact produced from CI matches the manual `Scripts/package-windows-installer.ps1` output.

---

## Execution Handoff

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks, ~15 task dispatches plus reviews. Tasks 1, 3, 5, 9, 14 are quick wins; Task 7 (Settings) and Task 15 (cutover) are the largest.

2. **Inline Execution** — execute tasks here with manual checkpoints. Recommended if you want to see/approve each visual change before moving on.

Either way, after Task 15 the WinUI 3 shell is THE CodexBar Windows app. Phase 4 (history/sparklines), Phase 5 (widget board), and Phase 6 (MSIX + Store) become independent follow-up plans.
