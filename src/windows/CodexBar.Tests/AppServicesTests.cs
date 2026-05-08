using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Settings;
using CodexBar.WinApp;

namespace CodexBar.Tests;

[TestClass]
public sealed class AppServicesTests
{
    [TestMethod]
    public async Task CreatesCodexAndClaudeProvidersAndStoresMissingCredentialSnapshots()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
            var settings = AppSettings.Default with { ClaudeManualCookieHeader = null };
            using var services = new AppServices(paths, settings);

            CollectionAssert.AreEqual(
                new[] { UsageProvider.Codex, UsageProvider.Claude, UsageProvider.Cursor, UsageProvider.Gemini },
                services.Providers.Select(provider => provider.Provider).ToArray());
            Assert.AreSame(paths, services.Paths);
            Assert.AreEqual(settings, services.Settings);

            await services.Scheduler.RefreshAllAsync(CancellationToken.None);

            var snapshots = services.Store.All();
            Assert.AreEqual(4, snapshots.Count);
            Assert.IsTrue(snapshots.All(snapshot => snapshot.IsStale));
            Assert.IsTrue(snapshots.All(snapshot => snapshot.SourceLabel == "none"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void OmitsDisabledProviders()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
            using var codexOnly = new AppServices(paths, AppSettings.Default with
            {
                ClaudeEnabled = false,
                CursorEnabled = false,
                GeminiEnabled = false
            });
            using var claudeOnly = new AppServices(paths, AppSettings.Default with
            {
                CodexEnabled = false,
                CursorEnabled = false,
                GeminiEnabled = false
            });

            CollectionAssert.AreEqual(
                new[] { UsageProvider.Codex },
                codexOnly.Providers.Select(provider => provider.Provider).ToArray());
            CollectionAssert.AreEqual(
                new[] { UsageProvider.Claude },
                claudeOnly.Providers.Select(provider => provider.Provider).ToArray());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void OmitsDisabledPreviewProviders()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
            using var services = new AppServices(paths, AppSettings.Default with
            {
                CursorEnabled = false,
                GeminiEnabled = false
            });

            CollectionAssert.AreEqual(
                new[] { UsageProvider.Codex, UsageProvider.Claude },
                services.Providers.Select(provider => provider.Provider).ToArray());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task TestProviderRefreshesOneProviderAndStoresResult()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
            using var services = new AppServices(paths, AppSettings.Default);

            var snapshot = await services.TestProviderAsync(UsageProvider.Cursor, CancellationToken.None);

            Assert.AreEqual(UsageProvider.Cursor, snapshot.Provider);
            Assert.IsTrue(snapshot.IsStale);
            StringAssert.Contains(snapshot.ErrorMessage!, "Cursor cookie");
            Assert.AreSame(snapshot, services.Store.Get(UsageProvider.Cursor));
            Assert.IsNull(services.Store.Get(UsageProvider.Codex));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void BuildsTrayDisplayFromMostUsedWindow()
    {
        var snapshots = new[]
        {
            new UsageSnapshot(
                UsageProvider.Codex,
                "Codex",
                DateTimeOffset.Now,
                new[]
                {
                    new RateWindow("session", "Session", 10, null, null),
                    new RateWindow("weekly", "Weekly", 95, null, null)
                },
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "test",
                null,
                false)
        };

        var display = App.BuildTrayDisplay(snapshots);

        Assert.AreEqual(95, display.Percent);
        Assert.IsFalse(display.IsStale);
    }

    [TestMethod]
    public void FiltersSnapshotsForEnabledProviders()
    {
        var snapshots = new[]
        {
            Snapshot(UsageProvider.Codex),
            Snapshot(UsageProvider.Claude),
            Snapshot(UsageProvider.Cursor),
            Snapshot(UsageProvider.Gemini)
        };
        var settings = AppSettings.Default with { ClaudeEnabled = false, GeminiEnabled = false };

        var filtered = App.FilterSnapshotsForSettings(snapshots, settings);

        Assert.AreEqual(2, filtered.Count);
        Assert.AreEqual(UsageProvider.Codex, filtered[0].Provider);
        Assert.AreEqual(UsageProvider.Cursor, filtered[1].Provider);
    }

    [TestMethod]
    public void EnsuresEnabledProvidersHaveSnapshotsAfterSettingsChange()
    {
        var snapshots = new[]
        {
            Snapshot(UsageProvider.Codex),
            Snapshot(UsageProvider.Claude)
        };

        var seeded = App.EnsureEnabledProviderSnapshots(snapshots, AppSettings.Default);

        CollectionAssert.AreEqual(
            new[] { UsageProvider.Codex, UsageProvider.Claude, UsageProvider.Cursor, UsageProvider.Gemini },
            seeded.Select(snapshot => snapshot.Provider).ToArray());
        Assert.AreEqual("Refreshing usage...", seeded.Single(snapshot => snapshot.Provider == UsageProvider.Cursor).ErrorMessage);
        Assert.AreEqual("Refreshing usage...", seeded.Single(snapshot => snapshot.Provider == UsageProvider.Gemini).ErrorMessage);
    }

    [TestMethod]
    public async Task LoadsDefaultSettingsWhenSettingsFileIsCorrupt()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
            Directory.CreateDirectory(Path.GetDirectoryName(paths.SettingsFile)!);
            await File.WriteAllTextAsync(paths.SettingsFile, "{ definitely not json");

            var settings = await App.LoadSettingsOrDefaultAsync(paths, CancellationToken.None);

            Assert.AreEqual(AppSettings.Default, settings);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static UsageSnapshot Snapshot(UsageProvider provider) =>
        new(
            provider,
            provider.ToString(),
            DateTimeOffset.Now,
            Array.Empty<RateWindow>(),
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
}
