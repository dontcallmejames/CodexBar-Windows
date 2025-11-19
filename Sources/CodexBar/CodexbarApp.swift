import SwiftUI
import Security
import AppKit
import Combine

@main
struct CodexBarApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var settings = SettingsStore()
    @StateObject private var store: UsageStore
    private let account: AccountInfo

    init() {
        let settings = SettingsStore()
        // Guard: always keep at least one provider visible so the app shows an icon.
        if !settings.showCodexUsage && !settings.showClaudeUsage {
            settings.showCodexUsage = true
        }
        let fetcher = UsageFetcher()
        self.account = fetcher.loadAccountInfo()
        _settings = StateObject(wrappedValue: settings)
        _store = StateObject(wrappedValue: UsageStore(fetcher: fetcher, settings: settings))
        self.appDelegate.configure(store: _store.wrappedValue, settings: settings, account: self.account)
    }

    @SceneBuilder
    var body: some Scene {
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

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    let updaterController: UpdaterProviding = makeUpdaterController()
    private var statusController: StatusItemController?

    func configure(store: UsageStore, settings: SettingsStore, account: AccountInfo) {
        self.statusController = StatusItemController(
            store: store,
            settings: settings,
            account: account,
            updater: self.updaterController)
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        // If not configured yet (shouldn't happen), create a minimal controller.
        if self.statusController == nil {
            let settings = SettingsStore()
            let fetcher = UsageFetcher()
            let account = fetcher.loadAccountInfo()
            let store = UsageStore(fetcher: fetcher, settings: settings)
            self.statusController = StatusItemController(
                store: store,
                settings: settings,
                account: account,
                updater: self.updaterController)
        }
    }
}

extension CodexBarApp {
    private var codexSnapshot: UsageSnapshot? { self.store.snapshot(for: .codex) }
    private var claudeSnapshot: UsageSnapshot? { self.store.snapshot(for: .claude) }
    private var codexShouldAnimate: Bool {
        self.settings.showCodexUsage && self.codexSnapshot == nil && !self.store.isStale(provider: .codex)
    }
    private var claudeShouldAnimate: Bool {
        self.settings.showClaudeUsage && self.claudeSnapshot == nil && !self.store.isStale(provider: .claude)
    }
}

// MARK: - Status item controller (AppKit)

@MainActor
final class StatusItemController: NSObject, NSMenuDelegate {
    private let store: UsageStore
    private let settings: SettingsStore
    private let account: AccountInfo
    private let updater: UpdaterProviding
    private let codexItem: NSStatusItem
    private let claudeItem: NSStatusItem
    private var cancellables = Set<AnyCancellable>()

    init(store: UsageStore, settings: SettingsStore, account: AccountInfo, updater: UpdaterProviding) {
        self.store = store
        self.settings = settings
        self.account = account
        self.updater = updater
        let bar = NSStatusBar.system
        self.codexItem = bar.statusItem(withLength: NSStatusItem.variableLength)
        self.claudeItem = bar.statusItem(withLength: NSStatusItem.variableLength)
        super.init()
        self.wireBindings()
        self.updateIcons()
        self.updateVisibility()
        self.installMenus()
    }

    private func wireBindings() {
        self.store.objectWillChange
            .receive(on: RunLoop.main)
            .sink { [weak self] _ in self?.updateIcons() }
            .store(in: &self.cancellables)

        self.settings.objectWillChange
            .receive(on: RunLoop.main)
            .sink { [weak self] _ in self?.updateVisibility() }
            .store(in: &self.cancellables)
    }

    private func installMenus() {
        self.codexItem.menu = self.makeMenu(for: .codex)
        self.claudeItem.menu = self.makeMenu(for: .claude)
        self.codexItem.menu?.delegate = self
        self.claudeItem.menu?.delegate = self
    }

    private func updateIcons() {
        if let button = self.codexItem.button {
            button.image = IconRenderer.makeIcon(
                primaryRemaining: self.store.snapshot(for: .codex)?.primary.remainingPercent,
                weeklyRemaining: self.store.snapshot(for: .codex)?.secondary.remainingPercent,
                creditsRemaining: self.store.credits?.remaining,
                stale: self.store.isStale(provider: .codex),
                style: .codex)
            button.target = self
            button.action = #selector(showCodexMenu)
        }
        if let button = self.claudeItem.button {
            button.image = IconRenderer.makeIcon(
                primaryRemaining: self.store.snapshot(for: .claude)?.primary.remainingPercent,
                weeklyRemaining: self.store.snapshot(for: .claude)?.secondary.remainingPercent,
                creditsRemaining: nil,
                stale: self.store.isStale(provider: .claude),
                style: .claude)
            button.target = self
            button.action = #selector(showClaudeMenu)
        }
    }

    private func updateVisibility() {
        self.codexItem.isVisible = self.settings.showCodexUsage
        self.claudeItem.isVisible = self.settings.showClaudeUsage
        if !self.settings.showCodexUsage && !self.settings.showClaudeUsage {
            self.settings.showCodexUsage = true
            self.codexItem.isVisible = true
        }
    }

    @objc private func showCodexMenu() {
        guard let button = self.codexItem.button else { return }
        self.codexItem.menu = self.makeMenu(for: .codex)
        self.codexItem.menu?.popUp(positioning: nil, at: NSPoint(x: 0, y: button.bounds.height - 3), in: button)
    }

    @objc private func showClaudeMenu() {
        guard let button = self.claudeItem.button else { return }
        self.claudeItem.menu = self.makeMenu(for: .claude)
        self.claudeItem.menu?.popUp(positioning: nil, at: NSPoint(x: 0, y: button.bounds.height - 3), in: button)
    }

    func menuWillOpen(_ menu: NSMenu) {
        if menu == self.codexItem.menu {
            self.codexItem.menu = self.makeMenu(for: .codex)
            self.codexItem.menu?.delegate = self
        } else if menu == self.claudeItem.menu {
            self.claudeItem.menu = self.makeMenu(for: .claude)
            self.claudeItem.menu?.delegate = self
        }
    }

    private func makeMenu(for provider: UsageProvider) -> NSMenu {
        let menu = NSMenu()
        menu.autoenablesItems = false

        addUsageSection(menu: menu, provider: provider)
        menu.addItem(.separator())
        addCredits(menu: menu, provider: provider)
        menu.addItem(.separator())
        addAccount(menu: menu, provider: provider)
        menu.addItem(.separator())
        addActions(menu: menu)
        menu.addItem(.separator())
        addMeta(menu: menu)
        return menu
    }

    // MARK: - Menu builders (pure AppKit)

    private func addUsageSection(menu: NSMenu, provider: UsageProvider) {
        let snap = self.store.snapshot(for: provider)
        switch provider {
        case .codex:
            menu.addItem(title: "Codex · 5h limit", isBold: true)
            if let snap {
                menu.addItem(title: UsageFormatter.usageLine(remaining: snap.primary.remainingPercent, used: snap.primary.usedPercent))
                if let reset = snap.primary.resetDescription { menu.addItem(title: "Resets \(reset)") }
                menu.addItem(title: "Codex · Weekly limit", isBold: true)
                menu.addItem(title: UsageFormatter.usageLine(remaining: snap.secondary.remainingPercent, used: snap.secondary.usedPercent))
                if let reset = snap.secondary.resetDescription { menu.addItem(title: "Resets \(reset)") }
                menu.addItem(title: UsageFormatter.updatedString(from: snap.updatedAt))
            } else {
                menu.addItem(title: "No usage yet")
                addError(menu: menu, error: self.store.lastCodexError)
            }
        case .claude:
            menu.addItem(title: "Claude · Session", isBold: true)
            if let snap {
                menu.addItem(title: UsageFormatter.usageLine(remaining: snap.primary.remainingPercent, used: snap.primary.usedPercent))
                if let reset = snap.primary.resetDescription { menu.addItem(title: "Resets \(reset)") }
                menu.addItem(title: "Claude · Weekly", isBold: true)
                menu.addItem(title: UsageFormatter.usageLine(remaining: snap.secondary.remainingPercent, used: snap.secondary.usedPercent))
                if let reset = snap.secondary.resetDescription { menu.addItem(title: "Resets \(reset)") }
                menu.addItem(title: UsageFormatter.updatedString(from: snap.updatedAt))
                if let email = snap.accountEmail { menu.addItem(title: "Account: \(email)") }
                if let org = snap.accountOrganization { menu.addItem(title: "Org: \(org)") }
            } else {
                menu.addItem(title: "No usage yet")
                addError(menu: menu, error: self.store.lastClaudeError)
            }
        }
    }

    private func addError(menu: NSMenu, error: String?) {
        guard let err = error, !err.isEmpty else { return }
        let truncated = err.count > 20 ? String(err.prefix(20)) + "…" : err
        let item = NSMenuItem(title: truncated, action: #selector(copyError(_:)), keyEquivalent: "")
        item.representedObject = err
        menu.addItem(item)
    }

    private func addCredits(menu: NSMenu, provider: UsageProvider) {
        guard provider == .codex else { return }
        if let credits = self.store.credits {
            menu.addItem(title: "Credits: \(UsageFormatter.creditsString(from: credits.remaining))")
            if let latest = credits.events.first {
                menu.addItem(title: "Last spend: \(UsageFormatter.creditEventSummary(latest))")
            }
        } else {
            menu.addItem(title: "Credits: sign in")
        }
    }

    private func addAccount(menu: NSMenu, provider: UsageProvider) {
        guard provider == .codex else { return }
        if let email = self.account.email {
            menu.addItem(title: "Codex account: \(email)")
        } else {
            menu.addItem(title: "Codex account: unknown")
        }
        if let plan = self.account.plan {
            menu.addItem(title: "Plan: \(plan.capitalized)")
        }
    }

    private func addActions(menu: NSMenu) {
        menu.addItem(NSMenuItem(title: "Refresh now", action: #selector(refreshNow), keyEquivalent: ""))
        menu.addItem(NSMenuItem(title: "Usage Dashboard", action: #selector(openDashboard), keyEquivalent: ""))
    }

    private func addMeta(menu: NSMenu) {
        let s = NSMenuItem(title: "Settings", action: #selector(openSettings), keyEquivalent: "")
        s.target = self
        menu.addItem(s)
        let a = NSMenuItem(title: "About CodexBar", action: #selector(openAbout), keyEquivalent: "")
        a.target = self
        menu.addItem(a)
        let q = NSMenuItem(title: "Quit", action: #selector(quit), keyEquivalent: "q")
        q.target = self
        menu.addItem(q)
    }

    // MARK: Actions
    @objc private func refreshNow() {
        Task { await self.store.refresh() }
    }

    @objc private func openDashboard() {
        if let url = URL(string: "https://chatgpt.com/codex/settings/usage") {
            NSWorkspace.shared.open(url)
        }
    }

    @objc private func openSettings() {
        NSApp.sendAction(Selector(("showPreferencesWindow:")), to: nil, from: nil)
    }

    @objc private func openAbout() {
        showAbout()
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }

    @objc private func copyError(_ sender: NSMenuItem) {
        if let err = sender.representedObject as? String {
            let pb = NSPasteboard.general
            pb.clearContents()
            pb.setString(err, forType: .string)
        }
    }
}

// MARK: - NSMenu helpers
private extension NSMenu {
    @discardableResult
    func addItem(title: String, isBold: Bool = false) -> NSMenuItem {
        let item = NSMenuItem(title: title, action: nil, keyEquivalent: "")
        if isBold {
            let font = NSFont.systemFont(ofSize: NSFont.systemFontSize, weight: .bold)
            item.attributedTitle = NSAttributedString(string: title, attributes: [.font: font])
        }
        self.addItem(item)
        return item
    }
}
