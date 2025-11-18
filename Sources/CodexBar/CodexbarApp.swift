import SwiftUI
import Security

@main
struct CodexBarApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var settings = SettingsStore()
    @StateObject private var store: UsageStore
    private let account: AccountInfo
    private var hasAccount: Bool {
        self.account.email != nil || self.account.plan != nil
    }

    init() {
        let settings = SettingsStore()
        if !settings.showCodexUsage && !settings.showClaudeUsage {
            settings.showCodexUsage = true
        }
        let fetcher = UsageFetcher()
        self.account = fetcher.loadAccountInfo()
        _settings = StateObject(wrappedValue: settings)
        _store = StateObject(wrappedValue: UsageStore(fetcher: fetcher, settings: settings))
    }

    @SceneBuilder
    var body: some Scene {
        MenuBarExtra(
            isInserted: .constant(true)) {
            MenuContent(
                store: self.store,
                settings: self.settings,
                account: self.account,
                updater: self.appDelegate.updaterController,
                provider: .codex)
        } label: {
            let showCodex = self.settings.showCodexUsage
            let showClaude = self.settings.showClaudeUsage
            if showCodex && showClaude {
                HStack(spacing: 2) {
                    IconView(
                        snapshot: self.codexSnapshot,
                        creditsRemaining: self.store.credits?.remaining,
                        isStale: self.store.isStale(provider: .codex),
                        showLoadingAnimation: self.codexShouldAnimate,
                        style: .codex)
                    IconView(
                        snapshot: self.claudeSnapshot,
                        creditsRemaining: nil,
                        isStale: self.store.isStale(provider: .claude),
                        showLoadingAnimation: self.claudeShouldAnimate,
                        style: .claude)
                }
                .padding(.horizontal, -2)
            } else if showClaude {
                IconView(
                    snapshot: self.claudeSnapshot,
                    creditsRemaining: nil,
                    isStale: self.store.isStale(provider: .claude),
                    showLoadingAnimation: self.claudeShouldAnimate,
                    style: .claude)
            } else {
                IconView(
                    snapshot: self.codexSnapshot,
                    creditsRemaining: self.store.credits?.remaining,
                    isStale: self.store.isStale(provider: .codex),
                    showLoadingAnimation: self.codexShouldAnimate,
                    style: .codex)
            }
        }

        Settings { EmptyView() }
    }
}

// MARK: - Updater abstraction

@MainActor
protocol UpdaterProviding: AnyObject {
    var automaticallyChecksForUpdates: Bool { get set }
    var isAvailable: Bool { get }
    func checkForUpdates(_ sender: Any?)
}

// No-op updater used for debug builds and non-bundled runs to suppress Sparkle dialogs.
final class DisabledUpdaterController: UpdaterProviding {
    var automaticallyChecksForUpdates: Bool = false
    let isAvailable: Bool = false
    func checkForUpdates(_ sender: Any?) {}
}

#if canImport(Sparkle) && ENABLE_SPARKLE
import Sparkle
extension SPUStandardUpdaterController: UpdaterProviding {
    var automaticallyChecksForUpdates: Bool {
        get { self.updater.automaticallyChecksForUpdates }
        set { self.updater.automaticallyChecksForUpdates = newValue }
    }

    var isAvailable: Bool { true }
}

private func isDeveloperIDSigned(bundleURL: URL) -> Bool {
    var staticCode: SecStaticCode?
    guard SecStaticCodeCreateWithPath(bundleURL as CFURL, SecCSFlags(), &staticCode) == errSecSuccess,
          let code = staticCode else { return false }

    var infoCF: CFDictionary?
    guard SecCodeCopySigningInformation(code, SecCSFlags(rawValue: kSecCSSigningInformation), &infoCF) == errSecSuccess,
          let info = infoCF as? [String: Any],
          let certs = info[kSecCodeInfoCertificates as String] as? [SecCertificate],
          let leaf = certs.first else { return false }

    if let summary = SecCertificateCopySubjectSummary(leaf) as String? {
        return summary.hasPrefix("Developer ID Application:")
    }
    return false
}

private func makeUpdaterController() -> UpdaterProviding {
    let bundleURL = Bundle.main.bundleURL
    let isBundledApp = bundleURL.pathExtension == "app"
    guard isBundledApp, isDeveloperIDSigned(bundleURL: bundleURL) else { return DisabledUpdaterController() }

    let controller = SPUStandardUpdaterController(
        startingUpdater: false,
        updaterDelegate: nil,
        userDriverDelegate: nil)
    controller.updater.automaticallyChecksForUpdates = false
    controller.start()
    return controller
}
#else
private func makeUpdaterController() -> UpdaterProviding {
    DisabledUpdaterController()
}
#endif

final class AppDelegate: NSObject, NSApplicationDelegate {
    let updaterController: UpdaterProviding = makeUpdaterController()
}

extension CodexBarApp {
    private var shouldAnimateIcon: Bool {
        guard self.hasAccount else { return false }
        return self.displayPrimarySnapshot == nil && !self.displayIsStale
    }

    private var codexSnapshot: UsageSnapshot? { self.store.snapshot(for: .codex) }
    private var claudeSnapshot: UsageSnapshot? { self.store.snapshot(for: .claude) }
    private var codexShouldAnimate: Bool {
        self.hasAccount && self.settings.showCodexUsage && self.codexSnapshot == nil && !self.store.isStale(provider: .codex)
    }
    private var claudeShouldAnimate: Bool {
        self.hasAccount && self.settings.showClaudeUsage && self.claudeSnapshot == nil && !self.store.isStale(provider: .claude)
    }

    private var displayPrimarySnapshot: UsageSnapshot? {
        if self.settings.showCodexUsage, let snap = self.codexSnapshot { return snap }
        if self.settings.showClaudeUsage, let snap = self.claudeSnapshot { return snap }
        return nil
    }

    private var displayIsStale: Bool {
        if self.settings.showCodexUsage, self.store.isStale(provider: .codex) {
            return true
        }
        if self.settings.showClaudeUsage, self.store.isStale(provider: .claude) {
            return true
        }
        return false
    }

    private var displayStyle: IconStyle {
        (self.settings.showCodexUsage && self.settings.showClaudeUsage) ? .combined
        : (self.settings.showClaudeUsage ? .claude : .codex)
    }
}
