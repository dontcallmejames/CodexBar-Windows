# WinUI 3 Migration Phase 2 — Shell Spike

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a new `CodexBar.WinUI` project — a WinUI 3 / Windows App SDK app that boots a system-tray icon and a Mica-backed popover that reads usage from the existing `CodexBar.Core`. The spike should make the look-and-feel decision concrete: it must visibly feel like a piece of Windows 11, react to theme/accent changes live, and use Fluent / Segoe Fluent Icons / Mica backdrop. Settings, dock, history, first-run, and update are out of scope for the spike — Phase 3 picks them up.

**Architecture:** Side-by-side with the existing WPF app — both reference `CodexBar.Core` and `CodexBar.Tray`. The WinUI shell is built and run separately. Single-instance enforcement, packaged identity, immersive title bars, theme-listener, system backdrop are all wired from day one because they shape the architecture. View models use `CommunityToolkit.Mvvm` `[ObservableProperty]` source generators (smaller than the current hand-rolled `INotifyPropertyChanged`). Tray via `H.NotifyIcon.WinUI`. Window helpers via `WinUIEx`. Settings rows (used in Phase 3) via `CommunityToolkit.WinUI.Controls.SettingsControls`.

**Tech Stack:** .NET 9, Windows App SDK 1.6+ (self-contained), WinUI 3, C# (latest), `H.NotifyIcon.WinUI` 2.x, `WinUIEx` 2.x, `CommunityToolkit.Mvvm` 8.3+, `CommunityToolkit.WinUI.Controls.SettingsControls` 8.x, MSTest. MSIX packaging via single-project packaging.

**Build command (spike):** `C:\tmp\dotnet\dotnet.exe build src\windows\CodexBar.Windows.sln --configuration Release`
**Run command (spike, unpackaged for fast iteration):** `C:\tmp\dotnet\dotnet.exe run --project src\windows\CodexBar.WinUI\CodexBar.WinUI.csproj`

**Phase 1 dependency:** This plan assumes Phase 1 is merged (specifically: `ProviderRefreshStateRegistry` and `AppServices` are available, and Hosting/DI packages are referenced). If Phase 1 is not yet done, run Phase 1 Task 1–5 first so `CodexBar.Core` is in the right shape.

---

## File Structure

**Create:**
- `src/windows/CodexBar.WinUI/CodexBar.WinUI.csproj` — WinUI 3 app project (single-project MSIX packaged)
- `src/windows/CodexBar.WinUI/Package.appxmanifest` — MSIX manifest with packaged identity
- `src/windows/CodexBar.WinUI/app.manifest` — DPI awareness, supported OS versions
- `src/windows/CodexBar.WinUI/App.xaml` — application resources, theming
- `src/windows/CodexBar.WinUI/App.xaml.cs` — boot host, single-instance, tray, theme listener
- `src/windows/CodexBar.WinUI/AppHostBuilder.cs` — DI wiring
- `src/windows/CodexBar.WinUI/Services/ThemeListener.cs` — wraps `UISettings.ColorValuesChanged` + `Application.RequestedTheme`
- `src/windows/CodexBar.WinUI/Services/SystemBackdropController.cs` — Mica / Mica Alt / Acrylic fallback
- `src/windows/CodexBar.WinUI/Services/TrayHost.cs` — H.NotifyIcon.WinUI lifecycle, left-click toggles popover
- `src/windows/CodexBar.WinUI/Services/PopoverPositioner.cs` — cursor- and taskbar-aware positioning (port of Phase 1 `WindowCoordinator.CalculatePopoverPosition`)
- `src/windows/CodexBar.WinUI/Views/PopoverWindow.xaml` — Mica popover, NavigationView pivot for providers
- `src/windows/CodexBar.WinUI/Views/PopoverWindow.xaml.cs`
- `src/windows/CodexBar.WinUI/Views/Controls/ProviderTab.xaml` — single-provider content
- `src/windows/CodexBar.WinUI/Views/Controls/ProviderTab.xaml.cs`
- `src/windows/CodexBar.WinUI/Views/Controls/MetricRow.xaml`
- `src/windows/CodexBar.WinUI/Views/Controls/MetricRow.xaml.cs`
- `src/windows/CodexBar.WinUI/ViewModels/PopoverViewModel.cs` — reuses logic from existing WPF `PopoverViewModel` but uses CT.Mvvm source-gen
- `src/windows/CodexBar.WinUI/ViewModels/ProviderTabViewModel.cs`
- `src/windows/CodexBar.WinUI/ViewModels/MetricViewModel.cs`
- `src/windows/CodexBar.WinUI/Assets/StoreLogo.png`, `Square150x150Logo.png`, `Square44x44Logo.png`, `Wide310x150Logo.png` — placeholder MSIX assets
- `src/windows/CodexBar.WinUI/Strings/en-us/Resources.resw` — first-class localization seam from day one
- `src/windows/CodexBar.Tests/WinUiPopoverViewModelTests.cs`
- `src/windows/CodexBar.Tests/WinUiThemeListenerTests.cs`
- `src/windows/CodexBar.Tests/WinUiPopoverPositionerTests.cs`

**Modify:**
- `src/windows/CodexBar.Windows.sln` — add new project
- `src/windows/Directory.Build.props` — add `WindowsAppSDKSelfContained` flag at solution level

**Reuse without modification:**
- All of `CodexBar.Core` (providers, mappers, settings, refresh)
- `CodexBar.Tray.MeterIconRenderer` (the badge-drawing code is plain `System.Drawing` — `H.NotifyIcon.WinUI` can consume the `System.Drawing.Icon` it produces)

---

### Task 1: Add the new project skeleton

**Files:**
- Create: `src/windows/CodexBar.WinUI/CodexBar.WinUI.csproj`
- Create: `src/windows/CodexBar.WinUI/Package.appxmanifest`
- Create: `src/windows/CodexBar.WinUI/app.manifest`
- Modify: `src/windows/CodexBar.Windows.sln`

- [ ] **Step 1.1: Author csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows10.0.22621.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
    <RootNamespace>CodexBar.WinUI</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <UseWinUI>true</UseWinUI>
    <EnableMsixTooling>true</EnableMsixTooling>
    <WindowsPackageType>MSIX</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <Platforms>x64;arm64</Platforms>
    <RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>
    <Nullable>enable</Nullable>
    <UseRidGraph>true</UseRidGraph>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.6.241114003" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.22621.756" />
    <PackageReference Include="H.NotifyIcon.WinUI" Version="2.2.0" />
    <PackageReference Include="WinUIEx" Version="2.5.1" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.3.2" />
    <PackageReference Include="CommunityToolkit.WinUI.Controls.SettingsControls" Version="8.1.240916" />
    <PackageReference Include="CommunityToolkit.WinUI.Behaviors" Version="8.1.240916" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\CodexBar.Core\CodexBar.Core.csproj" />
    <ProjectReference Include="..\CodexBar.Tray\CodexBar.Tray.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Manifest Include="$(ApplicationManifest)" />
  </ItemGroup>
</Project>
```

- [ ] **Step 1.2: app.manifest**

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="CodexBar.WinUI"/>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/PM</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2,PerMonitor</dpiAwareness>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
</assembly>
```

- [ ] **Step 1.3: Package.appxmanifest (skeleton)**

Use the `Identity` `Name="CodexBar.Preview"`, `Publisher="CN=CodexBar"`, `Version="0.26.0.0"`. Declare `Application` with `EntryPoint="CodexBar.WinUI.App"`. Mark capabilities: none (no network capability needed for an MSIX desktop-bridge app — the app uses HttpClient directly).

(Full manifest XML included in implementation; the developer should copy the WinUI 3 template manifest and adjust identity strings only.)

- [ ] **Step 1.4: Add to solution**

```bash
C:\tmp\dotnet\dotnet.exe sln src\windows\CodexBar.Windows.sln add src\windows\CodexBar.WinUI\CodexBar.WinUI.csproj
```

- [ ] **Step 1.5: Build empty project**

Run: `C:\tmp\dotnet\dotnet.exe build src\windows\CodexBar.WinUI\CodexBar.WinUI.csproj -c Release`
Expected: SUCCESS (no application code yet; just package restore + manifest validation).

- [ ] **Step 1.6: Commit**

```bash
git add src/windows/CodexBar.WinUI src/windows/CodexBar.Windows.sln
git commit -m "Add CodexBar.WinUI WinUI 3 project skeleton"
```

---

### Task 2: ThemeListener (testable, no UI dependency)

**Files:**
- Create: `src/windows/CodexBar.WinUI/Services/ThemeListener.cs`
- Create: `src/windows/CodexBar.Tests/WinUiThemeListenerTests.cs`

- [ ] **Step 2.1: Failing test**

```csharp
[TestMethod]
public void Theme_FollowsSystem_WhenSet()
{
    var systemReports = new Queue<ApplicationTheme>();
    systemReports.Enqueue(ApplicationTheme.Dark);
    var listener = new ThemeListener(() => systemReports.Dequeue());
    listener.UserPreference = ThemePreference.System;
    Assert.AreEqual(ApplicationTheme.Dark, listener.Effective);
}

[TestMethod]
public void Theme_OverridesSystem_WhenUserChoosesLight()
{
    var listener = new ThemeListener(() => ApplicationTheme.Dark);
    listener.UserPreference = ThemePreference.Light;
    Assert.AreEqual(ApplicationTheme.Light, listener.Effective);
}
```

- [ ] **Step 2.2: Implement**

```csharp
namespace CodexBar.WinUI.Services;

public enum ThemePreference { System, Light, Dark }

public sealed class ThemeListener
{
    private readonly Func<ApplicationTheme> probeSystem;
    private ThemePreference preference = ThemePreference.System;

    public ThemeListener(Func<ApplicationTheme> probeSystem)
    {
        this.probeSystem = probeSystem;
    }

    public ThemePreference UserPreference
    {
        get => preference;
        set { preference = value; Changed?.Invoke(this, EventArgs.Empty); }
    }

    public ApplicationTheme Effective => preference switch
    {
        ThemePreference.Light => ApplicationTheme.Light,
        ThemePreference.Dark => ApplicationTheme.Dark,
        _ => probeSystem(),
    };

    public event EventHandler? Changed;

    public void Refresh() => Changed?.Invoke(this, EventArgs.Empty);
}
```

(In production wiring, `App.xaml.cs` subscribes a `UISettings.ColorValuesChanged` handler that calls `Refresh()`. The probe in production reads `new UISettings().GetColorValue(UIColorType.Background)` and returns dark/light based on luminance.)

- [ ] **Step 2.3: PASS + Commit**

```bash
git commit -am "Add ThemeListener service"
```

---

### Task 3: ViewModels port (CommunityToolkit.Mvvm)

**Files:**
- Create: `src/windows/CodexBar.WinUI/ViewModels/PopoverViewModel.cs`
- Create: `src/windows/CodexBar.WinUI/ViewModels/ProviderTabViewModel.cs`
- Create: `src/windows/CodexBar.WinUI/ViewModels/MetricViewModel.cs`
- Create: `src/windows/CodexBar.Tests/WinUiPopoverViewModelTests.cs`

- [ ] **Step 3.1: Failing test (port the most valuable cases from PopoverViewModelTests)**

Copy at least 5 representative test cases from `PopoverViewModelTests.cs` and adapt the constructor calls. The behavior should be identical to the WPF VM — same input → same output.

- [ ] **Step 3.2: Implement using `[ObservableProperty]`**

```csharp
using CodexBar.Core.Models;
using CodexBar.Core.Refresh;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodexBar.WinUI.ViewModels;

public sealed partial class PopoverViewModel : ObservableObject
{
    private readonly ProviderRefreshStateRegistry refreshStates;
    private readonly DateTimeOffset now;

    [ObservableProperty] private UsageProvider activeProvider;
    [ObservableProperty] private ProviderTabViewModel? activeTab;
    [ObservableProperty] private string liveIndicatorText = string.Empty;

    public IReadOnlyList<ProviderTabViewModel> Tabs { get; }

    public PopoverViewModel(
        IReadOnlyList<UsageSnapshot> snapshots,
        UsageProvider initialProvider,
        bool showUsageAsUsed,
        ProviderRefreshStateRegistry refreshStates,
        DateTimeOffset? now = null)
    {
        this.refreshStates = refreshStates;
        this.now = now ?? DateTimeOffset.Now;
        Tabs = snapshots.Select(s => new ProviderTabViewModel(s, showUsageAsUsed)).ToArray();
        SelectProvider(initialProvider);
    }

    [RelayCommand]
    public void SelectProvider(UsageProvider provider)
    {
        ActiveProvider = provider;
        ActiveTab = Tabs.FirstOrDefault(t => t.Provider == provider);
        liveIndicatorText = BuildLiveIndicator();
        OnPropertyChanged(nameof(LiveIndicatorText));
    }

    private string BuildLiveIndicator()
    {
        var last = refreshStates.Get(ActiveProvider).LastSuccess;
        if (last is null) return "Live — refreshing…";
        var diff = now - last.Value;
        return $"Live • updated {Humanize(diff)} ago";
    }

    private static string Humanize(TimeSpan diff) => diff switch
    {
        { TotalSeconds: < 60 } => $"{(int)diff.TotalSeconds}s",
        { TotalMinutes: < 60 } => $"{(int)diff.TotalMinutes}m",
        _ => $"{(int)diff.TotalHours}h",
    };
}
```

`ProviderTabViewModel` and `MetricViewModel` similarly use `[ObservableProperty]` and mirror the existing WPF VMs' contracts.

- [ ] **Step 3.3: PASS**

- [ ] **Step 3.4: Commit**

```bash
git commit -am "Port popover view models to CT.Mvvm in CodexBar.WinUI"
```

---

### Task 4: PopoverPositioner

**Files:**
- Create: `src/windows/CodexBar.WinUI/Services/PopoverPositioner.cs`
- Create: `src/windows/CodexBar.Tests/WinUiPopoverPositionerTests.cs`

- [ ] **Step 4.1: Failing test**

Port `WpfShellTests` positioning cases as `PopoverPositionerTests`. The math (cursor-aware, screen-clamped) is identical.

- [ ] **Step 4.2: Implement**

`PopoverPositioner.Calculate(System.Drawing.Point cursor, Windows.Graphics.RectInt32 workArea, int width, int height) → (int x, int y)`. Pure function — exact port of Phase 1 `WindowCoordinator.CalculatePopoverPosition` adjusted for `RectInt32`.

- [ ] **Step 4.3: PASS + Commit**

---

### Task 5: PopoverWindow XAML + Mica

**Files:**
- Create: `src/windows/CodexBar.WinUI/Views/PopoverWindow.xaml`
- Create: `src/windows/CodexBar.WinUI/Views/PopoverWindow.xaml.cs`

- [ ] **Step 5.1: XAML skeleton**

```xml
<winui:Window xmlns:winui="using:Microsoft.UI.Xaml"
              xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
              x:Class="CodexBar.WinUI.Views.PopoverWindow">
  <Grid Padding="16" RowSpacing="12" RowDefinitions="Auto,*,Auto">
    <NavigationView Grid.Row="0"
                    PaneDisplayMode="Top"
                    IsBackButtonVisible="Collapsed"
                    IsSettingsVisible="False"
                    SelectedItem="{x:Bind ViewModel.ActiveProvider, Mode=TwoWay}">
      <NavigationView.MenuItems>
        <NavigationViewItem Content="Codex" Icon="{StaticResource CodexGlyph}" Tag="Codex"/>
        <NavigationViewItem Content="Claude" Tag="Claude"/>
        <NavigationViewItem Content="Cursor" Tag="Cursor"/>
        <NavigationViewItem Content="Gemini" Tag="Gemini"/>
      </NavigationView.MenuItems>
    </NavigationView>
    <ContentPresenter Grid.Row="1" Content="{x:Bind ViewModel.ActiveTab, Mode=OneWay}"/>
    <TextBlock Grid.Row="2"
               Text="{x:Bind ViewModel.LiveIndicatorText, Mode=OneWay}"
               Style="{ThemeResource CaptionTextBlockStyle}"
               Opacity="0.7"/>
  </Grid>
</winui:Window>
```

- [ ] **Step 5.2: Code-behind: Mica + immersive title bar + size**

```csharp
public sealed partial class PopoverWindow : Window
{
    public PopoverViewModel ViewModel { get; }
    private MicaController? micaController;
    private SystemBackdropConfiguration? backdropConfig;

    public PopoverWindow(PopoverViewModel viewModel, ThemeListener theme)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null);
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(380, 480));
        TrySetMica(theme);
        theme.Changed += (_, _) => ApplyTheme(theme.Effective);
        ApplyTheme(theme.Effective);
    }

    private void TrySetMica(ThemeListener theme)
    {
        if (!MicaController.IsSupported()) return;
        backdropConfig = new SystemBackdropConfiguration { IsInputActive = true };
        micaController = new MicaController { Kind = MicaKind.Base };
        micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        micaController.SetSystemBackdropConfiguration(backdropConfig);
    }

    private void ApplyTheme(ApplicationTheme effective)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = effective == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        }
        if (backdropConfig is not null)
        {
            backdropConfig.Theme = effective == ApplicationTheme.Dark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;
        }
    }
}
```

- [ ] **Step 5.3: ProviderTab + MetricRow user controls**

Each metric uses `ProgressBar` with `Foreground="{ThemeResource SystemAccentColor}"` so the user's accent color shows up. Use Fluent type ramp:
- Provider plan: `SubtitleTextBlockStyle`
- Metric label: `BodyTextBlockStyle`
- Metric value: `BodyStrongTextBlockStyle`
- Timestamps: `CaptionTextBlockStyle`

- [ ] **Step 5.4: Build (no run yet, no tray)**

Run: `C:\tmp\dotnet\dotnet.exe build src\windows\CodexBar.WinUI\CodexBar.WinUI.csproj -c Release`
Expected: SUCCESS.

- [ ] **Step 5.5: Commit**

```bash
git commit -am "Add WinUI 3 popover with Mica backdrop and live theme"
```

---

### Task 6: Tray + single-instance App boot

**Files:**
- Create: `src/windows/CodexBar.WinUI/App.xaml`
- Create: `src/windows/CodexBar.WinUI/App.xaml.cs`
- Create: `src/windows/CodexBar.WinUI/Services/TrayHost.cs`
- Create: `src/windows/CodexBar.WinUI/AppHostBuilder.cs`

- [ ] **Step 6.1: App.xaml**

```xml
<Application xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="CodexBar.WinUI.App">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

- [ ] **Step 6.2: App.xaml.cs (host bootstrap + single-instance)**

```csharp
public partial class App : Application
{
    private IHost? host;
    private TrayHost? tray;
    private PopoverWindow? popover;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var instance = AppInstance.FindOrRegisterForKey("CodexBar.WinUI");
        if (!instance.IsCurrent)
        {
            await instance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Process.GetCurrentProcess().Kill();
            return;
        }

        host = AppHostBuilder.Build();
        await host.StartAsync();
        tray = host.Services.GetRequiredService<TrayHost>();
        tray.LeftClick += (_, _) => TogglePopover();
        tray.Show();
        await host.Services.GetRequiredService<AppShellController>().StartAsync(default);
    }

    private void TogglePopover()
    {
        if (popover is { Visible: true })
        {
            popover.Close();
            popover = null;
            return;
        }
        popover = host!.Services.GetRequiredService<PopoverWindow>();
        var positioner = host.Services.GetRequiredService<PopoverPositioner>();
        var pos = positioner.CalculateForCursor(); // queries System.Drawing.Cursor + DisplayArea
        popover.AppWindow.Move(new Windows.Graphics.PointInt32(pos.x, pos.y));
        popover.Activate();
    }
}
```

- [ ] **Step 6.3: TrayHost using H.NotifyIcon.WinUI**

Listen to `LeftClickCommand`, expose `LeftClick` event. Wire its icon from `CodexBar.Tray.MeterIconRenderer.Render(...)` — same code as today.

- [ ] **Step 6.4: AppHostBuilder**

```csharp
public static class AppHostBuilder
{
    public static IHost Build()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IAppPaths, WindowsAppPaths>();
        builder.Services.AddSingleton(sp =>
        {
            var paths = sp.GetRequiredService<IAppPaths>();
            var store = new JsonSettingsStore(paths.SettingsFile);
            return store.LoadAsync(default).GetAwaiter().GetResult() ?? new AppSettings();
        });
        builder.Services.AddSingleton<AppServices>();
        builder.Services.AddSingleton<ThemeListener>(_ => new ThemeListener(SystemThemeProbe.Read));
        builder.Services.AddSingleton<PopoverPositioner>();
        builder.Services.AddSingleton<TrayHost>();
        builder.Services.AddTransient<PopoverViewModel>(sp =>
        {
            var services = sp.GetRequiredService<AppServices>();
            return new PopoverViewModel(services.Store.All(), UsageProvider.Codex,
                services.Settings.ShowUsageAsUsed, services.RefreshStates);
        });
        builder.Services.AddTransient<PopoverWindow>();
        builder.Services.AddSingleton<RefreshOrchestrator>();
        builder.Services.AddSingleton<AppShellController>();
        return builder.Build();
    }
}
```

- [ ] **Step 6.5: Build + run (unpackaged for fast iteration)**

Run unpackaged via:
```powershell
C:\tmp\dotnet\dotnet.exe run --project src\windows\CodexBar.WinUI\CodexBar.WinUI.csproj -c Release
```

Manual verification:
- Tray icon appears.
- Left-click toggles popover.
- Popover has Mica backdrop.
- Switching Windows light/dark in Settings live-updates the popover (without restart).
- Changing the system accent color live-updates the progress bars.
- The provider tabs show real data from your locally-signed-in providers.

- [ ] **Step 6.6: Commit**

```bash
git commit -am "Wire CodexBar.WinUI app: tray + popover + single-instance"
```

---

### Task 7: Package as MSIX, smoke-test installed

**Files:**
- Modify: `Scripts/package-windows-winui.ps1` (new helper paralleling `package-windows.ps1`)
- Modify: `Scripts/package-windows-installer.ps1` to detect WinUI build SKU (later)

- [ ] **Step 7.1: Build MSIX**

```powershell
C:\tmp\dotnet\dotnet.exe publish src\windows\CodexBar.WinUI\CodexBar.WinUI.csproj `
  -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true `
  -p:AppxPackageDir=dist\winui\
```

- [ ] **Step 7.2: Install + run**

Sideload the `.msix` via `Add-AppxPackage`. Verify:
- App appears in Start Menu under "CodexBar Preview."
- Settings → Apps shows the entry correctly with version/publisher.
- Uninstall works cleanly.

- [ ] **Step 7.3: Commit**

```bash
git commit -am "Add MSIX packaging script for WinUI spike"
```

---

### Task 8: Spike review checkpoint

This is a decision gate, not a code step.

- [ ] **Step 8.1: Capture screenshots**

Save `docs/screenshots/winui-spike-dark.png`, `winui-spike-light.png`. Compare side-by-side with current WPF popover. Note any divergence from intent ("Windows 11 native").

- [ ] **Step 8.2: Performance check**

- Cold start time (tray icon visible) target: < 1.5s
- Popover open animation: smooth at 60fps in DWM
- Memory after 1 hour idle: < 80 MB private bytes

- [ ] **Step 8.3: Decision**

Outcomes:
1. Spike confirms direction → write Phase 3 plan (parity: settings/dock/first-run/about/update/history).
2. Spike reveals blockers → document them in this file under a new "Findings" section and re-scope.

---

## Self-Review Checklist

- [ ] `CodexBar.WinUI.csproj` references `CodexBar.Core` and `CodexBar.Tray`, but `CodexBar.WinApp` does **not** reference WinUI (the two shells are independent).
- [ ] Theme + accent reacts to changes in `Settings > Personalization` without restart.
- [ ] Mica is visible on Windows 11 22H2+; fallback is acceptable on older builds.
- [ ] Tray left-click toggles popover; right-click is a `MenuFlyout`, not a Forms ContextMenu.
- [ ] Single-instance activation works (launching twice does not show two tray icons).
- [ ] Popover ViewModel logic is covered by tests ported from WPF VM.
- [ ] No reference to `System.Windows.Forms` inside `CodexBar.WinUI`.
- [ ] MSIX builds, installs, uninstalls cleanly.

---

## Execution Handoff

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks. Useful because tasks 1, 2, 4 are independent and parallelizable; tasks 5–7 must be sequential.
2. **Inline Execution** — execute in this session with a checkpoint after Task 6 (first runnable shell) before committing to Task 7 (packaging).
