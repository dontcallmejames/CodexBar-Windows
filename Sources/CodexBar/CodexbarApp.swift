import Sparkle
import SwiftUI

@main
struct CodexBarApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var settings = SettingsStore()
    @StateObject private var store: UsageStore
    private let account: AccountInfo
    @State private var isInserted = true

    private var hasAccount: Bool {
        self.account.email != nil || self.account.plan != nil
    }

    private var shouldAnimateIcon: Bool {
        self.hasAccount && self.store.lastError == nil && self.store.snapshot == nil
    }

    init() {
        let settings = SettingsStore()
        let fetcher = UsageFetcher()
        self.account = fetcher.loadAccountInfo()
        _settings = StateObject(wrappedValue: settings)
        _store = StateObject(wrappedValue: UsageStore(fetcher: fetcher, settings: settings))
    }

    var body: some Scene {
        MenuBarExtra(isInserted: self.$isInserted) {
            MenuContent(
                store: self.store,
                settings: self.settings,
                account: self.account,
                updater: self.appDelegate.updaterController)
        } label: {
            IconView(
                snapshot: self.store.snapshot,
                isStale: self.store.lastError != nil,
                showLoadingAnimation: self.shouldAnimateIcon)
        }
        Settings {
            EmptyView()
        }
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    let updaterController = SPUStandardUpdaterController(
        startingUpdater: true,
        updaterDelegate: nil,
        userDriverDelegate: nil)
}
