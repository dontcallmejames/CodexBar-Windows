import AppKit
import CodexBarCore
import Foundation
import Testing
@testable import CodexBar

@Suite(.serialized)
@MainActor
struct CodexSystemPromotionUITests {
    @Test
    func `settings system request promotes immediately`() async throws {
        let container = try CodexAccountPromotionTestContainer(
            suiteName: "CodexSystemPromotionUITests-settings-immediate")
        defer { container.tearDown() }

        let target = try container.createManagedAccount(
            persistedEmail: "managed@example.com",
            authAccountID: "acct-managed")
        try container.persistAccounts([target])
        _ = try container.writeLiveOAuthAuthFile(email: "live@example.com", accountID: "acct-live")

        let managedAccountCoordinator = ManagedCodexAccountCoordinator()
        let promotionCoordinator = CodexAccountPromotionCoordinator(
            service: container.makeService(),
            managedAccountCoordinator: managedAccountCoordinator)
        let pane = ProvidersPane(
            settings: container.settings,
            store: container.usageStore,
            managedCodexAccountCoordinator: managedAccountCoordinator,
            codexAccountPromotionCoordinator: promotionCoordinator)

        let managedVisibleAccountID = try #require(container.settings.codexVisibleAccountProjection.visibleAccounts
            .first(where: { $0.storedAccountID == target.id })?
            .id)

        await pane._test_requestCodexSystemVisibleAccount(id: managedVisibleAccountID)

        #expect(container.settings.codexActiveSource == .liveSystem)
        #expect(container.settings.codexVisibleAccountProjection.liveVisibleAccountID == managedVisibleAccountID)
        #expect(promotionCoordinator.userFacingError == nil)
    }

    @Test
    func `menu system request promotes immediately`() async throws {
        let container = try CodexAccountPromotionTestContainer(
            suiteName: "CodexSystemPromotionUITests-menu-immediate")
        defer { container.tearDown() }

        let target = try container.createManagedAccount(
            persistedEmail: "managed@example.com",
            authAccountID: "acct-managed")
        try container.persistAccounts([target])
        _ = try container.writeLiveOAuthAuthFile(email: "live@example.com", accountID: "acct-live")

        let managedAccountCoordinator = ManagedCodexAccountCoordinator()
        let promotionCoordinator = CodexAccountPromotionCoordinator(
            service: container.makeService(),
            managedAccountCoordinator: managedAccountCoordinator)
        let controller = StatusItemController(
            store: container.usageStore,
            settings: container.settings,
            account: UsageFetcher().loadAccountInfo(),
            updater: DisabledUpdaterController(),
            preferencesSelection: PreferencesSelection(),
            managedCodexAccountCoordinator: managedAccountCoordinator,
            codexAccountPromotionCoordinator: promotionCoordinator,
            statusBar: NSStatusBar())

        let item = NSMenuItem()
        item.representedObject = target.id.uuidString
        controller.requestCodexSystemPromotionFromMenu(item)

        let managedVisibleAccountID = try #require(container.settings.codexVisibleAccountProjection.visibleAccounts
            .first(where: { $0.storedAccountID == target.id })?
            .id)
        for _ in 0..<20
            where container.settings.codexVisibleAccountProjection.liveVisibleAccountID != managedVisibleAccountID
        {
            try await Task.sleep(for: .milliseconds(20))
        }

        #expect(container.settings.codexActiveSource == .liveSystem)
        #expect(container.settings.codexVisibleAccountProjection.liveVisibleAccountID == managedVisibleAccountID)
        #expect(promotionCoordinator.userFacingError == nil)
    }

    @Test
    func `menu codex login blocks system promotion while live reauthentication is running`() async {
        let settings = self.makeSettingsStore()
        let store = self.makeUsageStore(settings: settings)
        let managedAccountCoordinator = ManagedCodexAccountCoordinator()
        let promotionCoordinator = CodexAccountPromotionCoordinator(
            settingsStore: settings,
            usageStore: store,
            managedAccountCoordinator: managedAccountCoordinator)
        let blockingRunner = BlockingCodexAmbientLoginRunnerForSystemPromotionUITests()
        let controller = StatusItemController(
            store: store,
            settings: settings,
            account: UsageFetcher().loadAccountInfo(),
            updater: DisabledUpdaterController(),
            preferencesSelection: PreferencesSelection(),
            managedCodexAccountCoordinator: managedAccountCoordinator,
            codexAccountPromotionCoordinator: promotionCoordinator,
            statusBar: NSStatusBar())
        controller._test_codexAmbientLoginRunnerOverride = { timeout in
            await blockingRunner.run(timeout: timeout)
        }

        let item = NSMenuItem()
        item.representedObject = UsageProvider.codex.rawValue
        controller.runSwitchAccount(item)

        for _ in 0..<50 where !promotionCoordinator.isAuthenticatingLiveAccount {
            try? await Task.sleep(for: .milliseconds(20))
        }

        #expect(promotionCoordinator.isAuthenticatingLiveAccount)
        #expect(promotionCoordinator.isInteractionBlocked())

        await blockingRunner.resume()

        for _ in 0..<50 where promotionCoordinator.isAuthenticatingLiveAccount || controller.loginTask != nil {
            try? await Task.sleep(for: .milliseconds(20))
        }

        #expect(promotionCoordinator.isAuthenticatingLiveAccount == false)
    }

    @Test
    func `codex menu descriptor includes system account submenu`() throws {
        let settings = self.makeSettingsStore()
        let store = self.makeUsageStore(settings: settings)
        let managedAccountID = UUID()
        let managedStoreURL = try self.makeManagedAccountStoreURL(accounts: [
            ManagedCodexAccount(
                id: managedAccountID,
                email: "managed@example.com",
                managedHomePath: "/tmp/managed-home",
                createdAt: 1,
                updatedAt: 2,
                lastAuthenticatedAt: 2),
        ])
        defer {
            settings._test_managedCodexAccountStoreURL = nil
            settings._test_liveSystemCodexAccount = nil
            try? FileManager.default.removeItem(at: managedStoreURL)
        }

        settings._test_managedCodexAccountStoreURL = managedStoreURL
        settings._test_liveSystemCodexAccount = ObservedSystemCodexAccount(
            email: "live@example.com",
            codexHomePath: "/Users/test/.codex",
            observedAt: Date())

        let descriptor = MenuDescriptor.build(
            provider: .codex,
            store: store,
            settings: settings,
            account: UsageFetcher().loadAccountInfo(),
            managedCodexAccountCoordinator: ManagedCodexAccountCoordinator(),
            codexAccountPromotionCoordinator: CodexAccountPromotionCoordinator(
                settingsStore: settings,
                usageStore: store,
                managedAccountCoordinator: ManagedCodexAccountCoordinator()),
            updateReady: false)

        let submenu = try #require(descriptor.sections
            .flatMap(\.entries)
            .compactMap { entry -> (String, String?, [MenuDescriptor.SubmenuItem])? in
                guard case let .submenu(title, systemImageName, items) = entry else { return nil }
                return (title, systemImageName, items)
            }
            .first(where: { $0.0 == "System Account" }))

        #expect(submenu.1 == MenuDescriptor.MenuActionSystemImage.systemAccount.rawValue)
        #expect(submenu.2.count == 2)
        #expect(submenu.2.first(where: { $0.title == "live@example.com" })?.isChecked == true)
        #expect(submenu.2.first(where: { $0.title == "live@example.com" })?.isEnabled == false)
        #expect(submenu.2.first(where: { $0.title == "managed@example.com" })?.isEnabled == true)
        #expect(submenu.2.first(where: { $0.title == "managed@example.com" })?.action ==
            .requestCodexSystemPromotion(managedAccountID))
    }

    @Test
    func `system account submenu renders only account rows`() throws {
        StatusItemController.menuCardRenderingEnabled = false
        StatusItemController.menuRefreshEnabled = false

        let settings = self.makeSettingsStore()
        let store = self.makeUsageStore(settings: settings)
        let managedAccountID = UUID()
        let managedStoreURL = try self.makeManagedAccountStoreURL(accounts: [
            ManagedCodexAccount(
                id: managedAccountID,
                email: "managed@example.com",
                managedHomePath: "/tmp/managed-home",
                createdAt: 1,
                updatedAt: 2,
                lastAuthenticatedAt: 2),
        ])
        defer {
            settings._test_managedCodexAccountStoreURL = nil
            settings._test_liveSystemCodexAccount = nil
            try? FileManager.default.removeItem(at: managedStoreURL)
        }

        settings._test_managedCodexAccountStoreURL = managedStoreURL
        settings._test_liveSystemCodexAccount = ObservedSystemCodexAccount(
            email: "live@example.com",
            codexHomePath: "/Users/test/.codex",
            observedAt: Date())

        let controller = StatusItemController(
            store: store,
            settings: settings,
            account: UsageFetcher().loadAccountInfo(),
            updater: DisabledUpdaterController(),
            preferencesSelection: PreferencesSelection(),
            managedCodexAccountCoordinator: ManagedCodexAccountCoordinator(),
            codexAccountPromotionCoordinator: CodexAccountPromotionCoordinator(
                settingsStore: settings,
                usageStore: store,
                managedAccountCoordinator: ManagedCodexAccountCoordinator()),
            statusBar: NSStatusBar())

        let menu = controller.makeMenu(for: .codex)
        controller.menuWillOpen(menu)

        let systemAccountItem = try #require(menu.items.first(where: { $0.title == "System Account" }))
        let submenu = try #require(systemAccountItem.submenu)

        #expect(submenu.delegate == nil)
        #expect(submenu.items.map(\.title) == ["live@example.com", "managed@example.com"])
        #expect(submenu.items.count == 2)
        #expect(submenu.items[0].state == .on)
        #expect(submenu.items[1].state == .off)
    }

    private func makeSettingsStore() -> SettingsStore {
        let suite = "CodexSystemPromotionUITests-\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defaults.removePersistentDomain(forName: suite)
        let configStore = testConfigStore(suiteName: suite)
        let settings = SettingsStore(
            userDefaults: defaults,
            configStore: configStore,
            zaiTokenStore: NoopZaiTokenStore(),
            syntheticTokenStore: NoopSyntheticTokenStore(),
            codexCookieStore: InMemoryCookieHeaderStore(),
            claudeCookieStore: InMemoryCookieHeaderStore(),
            cursorCookieStore: InMemoryCookieHeaderStore(),
            opencodeCookieStore: InMemoryCookieHeaderStore(),
            factoryCookieStore: InMemoryCookieHeaderStore(),
            minimaxCookieStore: InMemoryMiniMaxCookieStore(),
            minimaxAPITokenStore: InMemoryMiniMaxAPITokenStore(),
            kimiTokenStore: InMemoryKimiTokenStore(),
            kimiK2TokenStore: InMemoryKimiK2TokenStore(),
            augmentCookieStore: InMemoryCookieHeaderStore(),
            ampCookieStore: InMemoryCookieHeaderStore(),
            copilotTokenStore: InMemoryCopilotTokenStore(),
            tokenAccountStore: InMemoryTokenAccountStore())
        settings.statusChecksEnabled = false
        settings.refreshFrequency = .manual
        settings.mergeIcons = false

        let registry = ProviderRegistry.shared
        for provider in UsageProvider.allCases {
            guard let metadata = registry.metadata[provider] else { continue }
            settings.setProviderEnabled(provider: provider, metadata: metadata, enabled: provider == .codex)
        }

        return settings
    }

    private func makeUsageStore(settings: SettingsStore) -> UsageStore {
        UsageStore(
            fetcher: UsageFetcher(),
            browserDetection: BrowserDetection(cacheTTL: 0),
            settings: settings,
            startupBehavior: .testing)
    }

    private func makeManagedAccountStoreURL(accounts: [ManagedCodexAccount]) throws -> URL {
        let storeURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let store = FileManagedCodexAccountStore(fileURL: storeURL)
        try store.storeAccounts(ManagedCodexAccountSet(
            version: FileManagedCodexAccountStore.currentVersion,
            accounts: accounts))
        return storeURL
    }
}

private actor BlockingCodexAmbientLoginRunnerForSystemPromotionUITests {
    private var waiters: [CheckedContinuation<CodexLoginRunner.Result, Never>] = []

    func run(timeout _: TimeInterval) async -> CodexLoginRunner.Result {
        await withCheckedContinuation { continuation in
            self.waiters.append(continuation)
        }
    }

    func resume() {
        let result = CodexLoginRunner.Result(outcome: .success, output: "ok")
        self.waiters.forEach { $0.resume(returning: result) }
        self.waiters.removeAll()
    }
}
