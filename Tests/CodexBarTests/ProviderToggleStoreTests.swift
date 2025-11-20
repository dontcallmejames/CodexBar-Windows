import Foundation
import Testing
@testable import CodexBar

@MainActor
@Suite
struct ProviderToggleStoreTests {
    @Test
    func defaultsMatchMetadata() {
        let defaults = UserDefaults(suiteName: "ProviderToggleStoreTests-defaults")!
        defaults.removePersistentDomain(forName: "ProviderToggleStoreTests-defaults")
        let store = ProviderToggleStore(userDefaults: defaults)
        let registry = ProviderRegistry.shared
        let codexMeta = registry.metadata[.codex]!
        let claudeMeta = registry.metadata[.claude]!

        #expect(store.isEnabled(metadata: codexMeta))
        #expect(!store.isEnabled(metadata: claudeMeta))
    }

    @Test
    func persistsChanges() {
        let suite = "ProviderToggleStoreTests-persist"
        let defaultsA = UserDefaults(suiteName: suite)!
        defaultsA.removePersistentDomain(forName: suite)
        let storeA = ProviderToggleStore(userDefaults: defaultsA)
        let registry = ProviderRegistry.shared
        let claudeMeta = registry.metadata[.claude]!

        storeA.setEnabled(true, metadata: claudeMeta)

        let defaultsB = UserDefaults(suiteName: suite)!
        let storeB = ProviderToggleStore(userDefaults: defaultsB)
        #expect(storeB.isEnabled(metadata: claudeMeta))
    }

    @Test
    func purgesLegacyKeys() {
        let suite = "ProviderToggleStoreTests-purge"
        let defaults = UserDefaults(suiteName: suite)!
        defaults.removePersistentDomain(forName: suite)
        defaults.set(false, forKey: "showCodexUsage")
        defaults.set(true, forKey: "showClaudeUsage")

        let store = ProviderToggleStore(userDefaults: defaults)
        store.purgeLegacyKeys()

        #expect(defaults.object(forKey: "showCodexUsage") == nil)
        #expect(defaults.object(forKey: "showClaudeUsage") == nil)
    }
}
