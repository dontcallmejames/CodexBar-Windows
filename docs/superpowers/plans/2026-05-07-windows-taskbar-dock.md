# Windows Taskbar Dock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the selected taskbar-adjacent always-visible dock for CodexBar usage on Windows 11.

**Architecture:** Add a focused `TaskbarDockViewModel` and `TaskbarDockWindow`, then wire `App` to show/update/position it from the existing shared snapshot store. Keep the existing `DockOverviewNearTaskbar` setting internally for compatibility, but expose it to users as `Show taskbar dock`.

**Tech Stack:** .NET 9, WPF, MSTest, existing CodexBar Windows core/view-model patterns.

---

## File Structure

- Create `src/windows/CodexBar.WinApp/ViewModels/TaskbarDockViewModel.cs`: formats enabled-provider snapshots into compact tiles.
- Create `src/windows/CodexBar.WinApp/Views/TaskbarDockWindow.xaml`: translucent always-on dock UI and context menu.
- Create `src/windows/CodexBar.WinApp/Views/TaskbarDockWindow.xaml.cs`: click/context-menu behavior.
- Create `src/windows/CodexBar.Tests/TaskbarDockViewModelTests.cs`: view-model formatting coverage.
- Modify `src/windows/CodexBar.Tests/WpfShellTests.cs`: dock position helper and XAML translucency coverage.
- Modify `src/windows/CodexBar.Tests/SettingsWindowTests.cs`: settings label coverage.
- Modify `src/windows/CodexBar.WinApp/App.xaml.cs`: replace docked overview wiring with taskbar dock wiring.
- Modify `src/windows/CodexBar.WinApp/Views/SettingsWindow.xaml`: rename visible checkbox label.
- Leave existing `DockedOverviewWindow` and `DockedOverviewViewModel` files in place for this task unless tests reveal they are unused enough to remove safely. The MVP should avoid a broad cleanup.

---

### Task 1: Taskbar Dock View Model

**Files:**
- Create: `src/windows/CodexBar.WinApp/ViewModels/TaskbarDockViewModel.cs`
- Create: `src/windows/CodexBar.Tests/TaskbarDockViewModelTests.cs`

- [ ] **Step 1: Write failing tests for compact tile formatting**

Create `src/windows/CodexBar.Tests/TaskbarDockViewModelTests.cs`:

```csharp
using CodexBar.Core.Models;
using CodexBar.WinApp.ViewModels;

namespace CodexBar.Tests;

[TestClass]
public sealed class TaskbarDockViewModelTests
{
    [TestMethod]
    public void BuildsTilesForProviderSnapshots()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1000);
        var snapshots = new[]
        {
            new UsageSnapshot(
                UsageProvider.Codex,
                "Codex",
                now,
                new[] { new RateWindow("session", "Session", 34, now.AddHours(2), null) },
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "test",
                null,
                false),
            new UsageSnapshot(
                UsageProvider.Claude,
                "Claude",
                now,
                new[] { new RateWindow("sonnet", "Sonnet", 1, now.AddDays(1), null) },
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "test",
                null,
                true)
        };

        var vm = new TaskbarDockViewModel(snapshots, showUsageAsUsed: true);

        Assert.AreEqual(2, vm.Tiles.Count);
        Assert.AreEqual("Codex", vm.Tiles[0].ProviderName);
        Assert.AreEqual("34% used", vm.Tiles[0].PercentText);
        Assert.AreEqual(34, vm.Tiles[0].ProgressPercent);
        Assert.AreEqual("#35D2C6", vm.Tiles[0].ProgressColor);
        Assert.IsFalse(vm.Tiles[0].IsStale);
        Assert.IsFalse(vm.Tiles[0].IsEmpty);
        Assert.AreEqual("Claude", vm.Tiles[1].ProviderName);
        Assert.AreEqual("1% used", vm.Tiles[1].PercentText);
        Assert.IsTrue(vm.Tiles[1].IsStale);
    }

    [TestMethod]
    public void ShowsEmptyTileWhenSnapshotHasNoWindows()
    {
        var snapshot = UsageSnapshot.MissingCredentials(
            UsageProvider.Gemini,
            "Gemini",
            "Refreshing usage...");

        var vm = new TaskbarDockViewModel(new[] { snapshot }, showUsageAsUsed: true);

        Assert.AreEqual(1, vm.Tiles.Count);
        Assert.AreEqual("Gemini", vm.Tiles[0].ProviderName);
        Assert.AreEqual("--", vm.Tiles[0].PercentText);
        Assert.AreEqual(0, vm.Tiles[0].ProgressPercent);
        Assert.AreEqual("#B8B2C8", vm.Tiles[0].ProgressColor);
        Assert.IsTrue(vm.Tiles[0].IsEmpty);
        Assert.IsTrue(vm.Tiles[0].IsStale);
    }

    [TestMethod]
    public void RespectsRemainingModeWhenShowUsageAsUsedIsFalse()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1000);
        var snapshot = new UsageSnapshot(
            UsageProvider.Cursor,
            "Cursor",
            now,
            new[] { new RateWindow("included", "Included", 18, now.AddDays(10), null) },
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "test",
            null,
            false);

        var vm = new TaskbarDockViewModel(new[] { snapshot }, showUsageAsUsed: false);

        Assert.AreEqual("82% left", vm.Tiles[0].PercentText);
        Assert.AreEqual(82, vm.Tiles[0].ProgressPercent);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter TaskbarDockViewModelTests --verbosity minimal
```

Expected: build fails because `TaskbarDockViewModel` does not exist.

- [ ] **Step 3: Implement the view model**

Create `src/windows/CodexBar.WinApp/ViewModels/TaskbarDockViewModel.cs`:

```csharp
using CodexBar.Core.Models;

namespace CodexBar.WinApp.ViewModels;

public sealed record TaskbarDockTileViewModel(
    UsageProvider Provider,
    string ProviderName,
    string PercentText,
    double ProgressPercent,
    string ProgressColor,
    bool IsStale,
    bool IsEmpty);

public sealed class TaskbarDockViewModel
{
    public TaskbarDockViewModel(IReadOnlyList<UsageSnapshot> snapshots, bool showUsageAsUsed)
    {
        Tiles = snapshots.Select(snapshot =>
        {
            var window = snapshot.Windows.FirstOrDefault();
            if (window is null)
            {
                return new TaskbarDockTileViewModel(
                    snapshot.Provider,
                    snapshot.DisplayName,
                    "--",
                    0,
                    "#B8B2C8",
                    true,
                    true);
            }

            var percent = Math.Round(showUsageAsUsed ? window.UsedPercent : window.PercentLeft);
            var suffix = showUsageAsUsed ? "used" : "left";
            return new TaskbarDockTileViewModel(
                snapshot.Provider,
                snapshot.DisplayName,
                $"{percent:0}% {suffix}",
                percent,
                ProgressColor(snapshot.Provider),
                snapshot.IsStale,
                false);
        }).ToArray();
    }

    public IReadOnlyList<TaskbarDockTileViewModel> Tiles { get; }

    public bool HasTiles => Tiles.Count > 0;

    private static string ProgressColor(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Codex => "#35D2C6",
            UsageProvider.Claude => "#FF8C42",
            UsageProvider.Cursor => "#7264B8",
            UsageProvider.Gemini => "#2F82FF",
            _ => "#35D2C6"
        };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter TaskbarDockViewModelTests --verbosity minimal
```

Expected: all `TaskbarDockViewModelTests` pass.

- [ ] **Step 5: Commit**

```powershell
git add src/windows/CodexBar.WinApp/ViewModels/TaskbarDockViewModel.cs src/windows/CodexBar.Tests/TaskbarDockViewModelTests.cs
git commit -m "Add taskbar dock view model"
```

---

### Task 2: Dock Position Calculation

**Files:**
- Modify: `src/windows/CodexBar.WinApp/App.xaml.cs`
- Modify: `src/windows/CodexBar.Tests/WpfShellTests.cs`

- [ ] **Step 1: Write failing position tests**

Append these tests to `WpfShellTests`:

```csharp
[TestMethod]
public void CalculatesTaskbarDockPositionNearBottomRight()
{
    var position = CodexBar.WinApp.App.CalculateTaskbarDockPosition(
        width: 320,
        height: 64,
        workArea: new System.Windows.Rect(0, 0, 2560, 1040));

    Assert.AreEqual(2224, position.Left);
    Assert.AreEqual(964, position.Top);
}

[TestMethod]
public void CalculatesTaskbarDockPositionWithMinimumMargins()
{
    var position = CodexBar.WinApp.App.CalculateTaskbarDockPosition(
        width: 600,
        height: 120,
        workArea: new System.Windows.Rect(100, 50, 700, 500));

    Assert.AreEqual(184, position.Left);
    Assert.AreEqual(418, position.Top);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "WpfShellTests.CalculatesTaskbarDockPositionNearBottomRight|WpfShellTests.CalculatesTaskbarDockPositionWithMinimumMargins" --verbosity minimal
```

Expected: build fails because `CalculateTaskbarDockPosition` does not exist.

- [ ] **Step 3: Add the positioning helper**

Add this method to `App.xaml.cs` near the other public positioning helpers:

```csharp
public static (double Left, double Top) CalculateTaskbarDockPosition(
    double width,
    double height,
    System.Windows.Rect workArea)
{
    const double margin = 16;
    const double taskbarGap = 12;
    var maxLeft = workArea.Right - width - margin;
    var maxTop = workArea.Bottom - height - taskbarGap;

    return (
        Math.Clamp(maxLeft, workArea.Left + margin, maxLeft),
        Math.Clamp(maxTop, workArea.Top + margin, maxTop));
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "WpfShellTests.CalculatesTaskbarDockPositionNearBottomRight|WpfShellTests.CalculatesTaskbarDockPositionWithMinimumMargins" --verbosity minimal
```

Expected: both tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/windows/CodexBar.WinApp/App.xaml.cs src/windows/CodexBar.Tests/WpfShellTests.cs
git commit -m "Add taskbar dock positioning"
```

---

### Task 3: Taskbar Dock Window

**Files:**
- Create: `src/windows/CodexBar.WinApp/Views/TaskbarDockWindow.xaml`
- Create: `src/windows/CodexBar.WinApp/Views/TaskbarDockWindow.xaml.cs`
- Modify: `src/windows/CodexBar.Tests/WpfShellTests.cs`

- [ ] **Step 1: Write failing XAML coverage test**

Append this test to `WpfShellTests`:

```csharp
[TestMethod]
public void TaskbarDockWindowUsesCompactTranslucentSurface()
{
    var xamlPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "CodexBar.WinApp",
        "Views",
        "TaskbarDockWindow.xaml"));

    var xaml = File.ReadAllText(xamlPath);

    StringAssert.Contains(xaml, "ShowInTaskbar=\"False\"");
    StringAssert.Contains(xaml, "Topmost=\"True\"");
    StringAssert.Contains(xaml, "Width=\"320\"");
    StringAssert.Contains(xaml, "{StaticResource CodexBarPanelBrush}");
    StringAssert.Contains(xaml, "Hide Taskbar Dock");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter WpfShellTests.TaskbarDockWindowUsesCompactTranslucentSurface --verbosity minimal
```

Expected: test fails because `TaskbarDockWindow.xaml` does not exist.

- [ ] **Step 3: Create the taskbar dock XAML**

Create `src/windows/CodexBar.WinApp/Views/TaskbarDockWindow.xaml`:

```xml
<Window x:Class="CodexBar.WinApp.Views.TaskbarDockWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        ResizeMode="NoResize"
        ShowInTaskbar="False"
        Topmost="True"
        Width="320"
        SizeToContent="Height"
        Background="Transparent"
        FontFamily="Segoe UI">
  <Window.Resources>
    <Style x:Key="DockProgressBar" TargetType="ProgressBar">
      <Setter Property="Height" Value="3" />
      <Setter Property="Background" Value="#17000000" />
      <Setter Property="BorderThickness" Value="0" />
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ProgressBar">
            <Grid x:Name="PART_Track">
              <Border Background="{TemplateBinding Background}" CornerRadius="2" />
              <Decorator x:Name="PART_Indicator" HorizontalAlignment="Left">
                <Border Background="{TemplateBinding Foreground}" CornerRadius="2" />
              </Decorator>
            </Grid>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>

  <Border CornerRadius="13"
          Background="{StaticResource CodexBarPanelBrush}"
          BorderBrush="#99FFFFFF"
          BorderThickness="1"
          Padding="10"
          MouseLeftButtonUp="Dock_MouseLeftButtonUp"
          SnapsToDevicePixels="True">
    <Border.ContextMenu>
      <ContextMenu>
        <MenuItem Header="Refresh" Click="Refresh_Click" />
        <MenuItem Header="Settings" Click="Settings_Click" />
        <Separator />
        <MenuItem Header="Hide Taskbar Dock" Click="HideDock_Click" />
      </ContextMenu>
    </Border.ContextMenu>
    <Border.Effect>
      <DropShadowEffect BlurRadius="18"
                        ShadowDepth="4"
                        Opacity="0.14"
                        Color="#000000" />
    </Border.Effect>

    <ItemsControl ItemsSource="{Binding Tiles}">
      <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
          <UniformGrid Rows="1" />
        </ItemsPanelTemplate>
      </ItemsControl.ItemsPanel>
      <ItemsControl.ItemTemplate>
        <DataTemplate>
          <Border Margin="3,0"
                  Padding="7,6"
                  CornerRadius="9"
                  Background="#66FFFFFF"
                  BorderBrush="#16000000"
                  BorderThickness="1">
            <Grid>
              <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
              </Grid.RowDefinitions>
              <TextBlock Text="{Binding ProviderName}"
                         FontSize="12"
                         FontWeight="SemiBold"
                         Foreground="{StaticResource CodexBarTextBrush}"
                         TextTrimming="CharacterEllipsis" />
              <TextBlock Grid.Row="1"
                         Text="{Binding PercentText}"
                         Margin="0,2,0,0"
                         FontSize="11"
                         Foreground="{StaticResource CodexBarMutedTextBrush}"
                         TextTrimming="CharacterEllipsis" />
              <ProgressBar Grid.Row="2"
                           Value="{Binding ProgressPercent}"
                           Maximum="100"
                           Margin="0,7,0,0"
                           Foreground="{Binding ProgressColor}"
                           Style="{StaticResource DockProgressBar}" />
            </Grid>
          </Border>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </Border>
</Window>
```

- [ ] **Step 4: Create the window code-behind**

Create `src/windows/CodexBar.WinApp/Views/TaskbarDockWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using CodexBar.WinApp.ViewModels;

namespace CodexBar.WinApp.Views;

public partial class TaskbarDockWindow : Window
{
    private readonly Action openPopover;
    private readonly Action refresh;
    private readonly Action openSettings;
    private readonly Action hideDock;

    public TaskbarDockWindow(
        TaskbarDockViewModel viewModel,
        Action openPopover,
        Action refresh,
        Action openSettings,
        Action hideDock)
    {
        InitializeComponent();
        DataContext = viewModel;
        this.openPopover = openPopover;
        this.refresh = refresh;
        this.openSettings = openSettings;
        this.hideDock = hideDock;
    }

    private void Dock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        openPopover();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        refresh();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        openSettings();
    }

    private void HideDock_Click(object sender, RoutedEventArgs e)
    {
        hideDock();
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter WpfShellTests.TaskbarDockWindowUsesCompactTranslucentSurface --verbosity minimal
```

Expected: test passes.

- [ ] **Step 6: Commit**

```powershell
git add src/windows/CodexBar.WinApp/Views/TaskbarDockWindow.xaml src/windows/CodexBar.WinApp/Views/TaskbarDockWindow.xaml.cs src/windows/CodexBar.Tests/WpfShellTests.cs
git commit -m "Add taskbar dock window"
```

---

### Task 4: Wire Dock Into App Lifecycle

**Files:**
- Modify: `src/windows/CodexBar.WinApp/App.xaml.cs`

- [ ] **Step 1: Replace the docked overview field**

In `App.xaml.cs`, replace:

```csharp
private DockedOverviewWindow? dockedOverview;
```

with:

```csharp
private TaskbarDockWindow? taskbarDock;
```

Replace this line in `OnExit`:

```csharp
dockedOverview?.Close();
```

with:

```csharp
System.Windows.SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
taskbarDock?.Close();
```

- [ ] **Step 2: Add work-area change handling**

After `ApplyStartupRegistration(settings);` in `OnStartup`, add:

```csharp
System.Windows.SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
```

Add this method to `App.xaml.cs`:

```csharp
private void SystemParameters_StaticPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(System.Windows.SystemParameters.WorkArea))
    {
        PositionTaskbarDock();
    }
}
```

- [ ] **Step 3: Rename update calls**

Replace every call to:

```csharp
UpdateDockedOverview();
```

with:

```csharp
UpdateTaskbarDock();
```

- [ ] **Step 4: Replace `UpdateDockedOverview` with dock update logic**

Replace the entire `UpdateDockedOverview` method with:

```csharp
private void UpdateTaskbarDock()
{
    if (services is null)
    {
        return;
    }

    if (!services.Settings.DockOverviewNearTaskbar)
    {
        taskbarDock?.Close();
        taskbarDock = null;
        return;
    }

    var viewModel = new TaskbarDockViewModel(
        services.Store.All(),
        services.Settings.ShowUsageAsUsed);
    if (!viewModel.HasTiles)
    {
        taskbarDock?.Close();
        taskbarDock = null;
        return;
    }

    if (taskbarDock is null)
    {
        taskbarDock = new TaskbarDockWindow(
            viewModel,
            ShowPopoverFromDock,
            RefreshNow,
            ShowSettings,
            HideTaskbarDock);
        taskbarDock.Closed += (_, _) => taskbarDock = null;
        taskbarDock.Show();
    }
    else
    {
        taskbarDock.DataContext = viewModel;
    }

    taskbarDock.UpdateLayout();
    PositionTaskbarDock();
}
```

- [ ] **Step 5: Add dock positioning and actions**

Add these methods near `PositionPopoverNearCursor`:

```csharp
private void PositionTaskbarDock()
{
    if (taskbarDock?.IsVisible != true)
    {
        return;
    }

    var width = taskbarDock.ActualWidth > 0 ? taskbarDock.ActualWidth : taskbarDock.Width;
    var height = taskbarDock.ActualHeight > 0 ? taskbarDock.ActualHeight : taskbarDock.Height;
    var position = CalculateTaskbarDockPosition(width, height, System.Windows.SystemParameters.WorkArea);
    taskbarDock.Left = position.Left;
    taskbarDock.Top = position.Top;
}

private void ShowPopoverFromDock()
{
    if (popover?.IsVisible == true)
    {
        popover.Activate();
        return;
    }

    ShowPopover();
}

private async void RefreshNow()
{
    if (services is null)
    {
        return;
    }

    await RefreshServicesAsync(services);
}

private async void HideTaskbarDock()
{
    if (services is null || settingsStore is null)
    {
        return;
    }

    var settings = services.Settings with { DockOverviewNearTaskbar = false };
    try
    {
        await settingsStore.SaveAsync(settings, shutdown.Token);
        ApplySettings(settings);
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
    {
        System.Windows.MessageBox.Show(
            error.Message,
            "CodexBar Settings",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }
}
```

- [ ] **Step 6: Build to catch WPF compile errors**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe build src\windows\CodexBar.Windows.sln --verbosity minimal
```

Expected: build succeeds.

- [ ] **Step 7: Run focused tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter "TaskbarDockViewModelTests|WpfShellTests" --verbosity minimal
```

Expected: all selected tests pass.

- [ ] **Step 8: Commit**

```powershell
git add src/windows/CodexBar.WinApp/App.xaml.cs
git commit -m "Wire taskbar dock into app"
```

---

### Task 5: Settings Label And Compatibility

**Files:**
- Modify: `src/windows/CodexBar.WinApp/Views/SettingsWindow.xaml`
- Modify: `src/windows/CodexBar.Tests/SettingsWindowTests.cs`

- [ ] **Step 1: Write failing settings label test**

Add this test to `SettingsWindowTests`:

```csharp
[TestMethod]
public void SettingsWindowLabelsDockAsTaskbarDock()
{
    var settingsXamlPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "CodexBar.WinApp",
        "Views",
        "SettingsWindow.xaml"));

    var settingsXaml = File.ReadAllText(settingsXamlPath);

    StringAssert.Contains(settingsXaml, "Show taskbar dock");
    StringAssert.Contains(settingsXaml, "IsChecked=\"{Binding DockOverviewNearTaskbar}\"");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter SettingsWindowTests.SettingsWindowLabelsDockAsTaskbarDock --verbosity minimal
```

Expected: test fails because the visible label still says `Dock overview near taskbar`.

- [ ] **Step 3: Update the settings label**

In `SettingsWindow.xaml`, replace:

```xml
<CheckBox Content="Dock overview near taskbar" IsChecked="{Binding DockOverviewNearTaskbar}" Margin="0,6" />
```

with:

```xml
<CheckBox Content="Show taskbar dock" IsChecked="{Binding DockOverviewNearTaskbar}" Margin="0,6" />
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Tests\CodexBar.Tests.csproj --filter SettingsWindowTests.SettingsWindowLabelsDockAsTaskbarDock --verbosity minimal
```

Expected: test passes.

- [ ] **Step 5: Commit**

```powershell
git add src/windows/CodexBar.WinApp/Views/SettingsWindow.xaml src/windows/CodexBar.Tests/SettingsWindowTests.cs
git commit -m "Rename dock setting label"
```

---

### Task 6: Final Verification And Package

**Files:**
- No source edits expected unless verification exposes a defect.

- [ ] **Step 1: Run full Debug tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --verbosity minimal
```

Expected: all tests pass.

- [ ] **Step 2: Run full Release tests**

Run:

```powershell
C:\tmp\dotnet\dotnet.exe test src\windows\CodexBar.Windows.sln --configuration Release --verbosity minimal
```

Expected: all tests pass.

- [ ] **Step 3: Build release package**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\package-windows.ps1 -DotNet C:\tmp\dotnet\dotnet.exe
```

Expected: zip and checksum are updated under `dist\windows`.

- [ ] **Step 4: Refresh portable test copy and smoke-launch**

Run:

```powershell
$out = 'C:\tmp\CodexBar-Windows-Portable'
Get-Process -Name CodexBar.WinApp -ErrorAction SilentlyContinue | Stop-Process -Force
$resolvedParent = Resolve-Path -LiteralPath 'C:\tmp'
$targetFull = [System.IO.Path]::GetFullPath($out)
if (-not $targetFull.StartsWith($resolvedParent.Path, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Refusing to remove unexpected path: $targetFull" }
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
C:\tmp\dotnet\dotnet.exe publish src\windows\CodexBar.WinApp\CodexBar.WinApp.csproj -c Release -r win-x64 --self-contained true -o $out --verbosity minimal
$exe = Join-Path $out 'CodexBar.WinApp.exe'
$file = Get-Item -LiteralPath $exe
$p = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 8
$alive = -not $p.HasExited
if ($alive) { Stop-Process -Id $p.Id -Force }
[pscustomobject]@{ Exe=$file.FullName; LastWriteTime=$file.LastWriteTime; StartedAndStayedAlive=$alive } | Format-List
```

Expected: `StartedAndStayedAlive : True`.

- [ ] **Step 5: Manual UI verification**

Run the fresh portable EXE:

```powershell
Start-Process -FilePath C:\tmp\CodexBar-Windows-Portable\CodexBar.WinApp.exe
```

Verify:

- Settings shows `Show taskbar dock`.
- Enabling the setting shows the dock above the taskbar near the tray edge.
- The dock shows one tile per enabled provider.
- Clicking the dock opens the full popover.
- Right-click > Settings opens settings.
- Right-click > Hide Taskbar Dock hides the dock and persists the setting.
- The dock does not obscure the bottom of the popover.

- [ ] **Step 6: Commit any verification fixes**

If Step 5 exposes a fix, commit it with a focused message:

```powershell
git add <changed-files>
git commit -m "Polish taskbar dock behavior"
```

If no source changes are required, skip this step.

- [ ] **Step 7: Push branch**

Run:

```powershell
git push origin main
```

Expected: all taskbar dock commits are pushed to `origin/main`.
