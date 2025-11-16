import AppKit
import Combine
import ServiceManagement
import Sparkle
import SwiftUI

// MARK: - Settings

enum RefreshFrequency: String, CaseIterable, Identifiable {
    case manual
    case oneMinute
    case twoMinutes
    case fiveMinutes

    var id: String { self.rawValue }

    var seconds: TimeInterval? {
        switch self {
        case .manual: nil
        case .oneMinute: 60
        case .twoMinutes: 120
        case .fiveMinutes: 300
        }
    }

    var label: String {
        switch self {
        case .manual: "Manual"
        case .oneMinute: "1 min"
        case .twoMinutes: "2 min"
        case .fiveMinutes: "5 min"
        }
    }
}

@MainActor
final class SettingsStore: ObservableObject {
    @Published var refreshFrequency: RefreshFrequency {
        didSet { UserDefaults.standard.set(self.refreshFrequency.rawValue, forKey: "refreshFrequency") }
    }

    @AppStorage("launchAtLogin") var launchAtLogin: Bool = false {
        // Keep SMLoginItem state in sync with the toggle.
        didSet { LaunchAtLoginManager.setEnabled(self.launchAtLogin) }
    }

    /// Hidden toggle to reveal debug-only menu items (enable via defaults write com.steipete.CodexBar debugMenuEnabled -bool YES).
    @AppStorage("debugMenuEnabled") var debugMenuEnabled: Bool = false

    init(userDefaults: UserDefaults = .standard) {
        let raw = userDefaults.string(forKey: "refreshFrequency") ?? RefreshFrequency.twoMinutes.rawValue
        self.refreshFrequency = RefreshFrequency(rawValue: raw) ?? .twoMinutes
        // Apply stored login preference immediately on launch.
        LaunchAtLoginManager.setEnabled(self.launchAtLogin)
    }
}

// MARK: - Usage Store

@MainActor
final class UsageStore: ObservableObject {
    @Published var snapshot: UsageSnapshot?
    @Published var lastError: String?
    @Published var isRefreshing = false

    private let fetcher: UsageFetcher
    private let settings: SettingsStore
    private var timerTask: Task<Void, Never>?
    private var cancellables = Set<AnyCancellable>()

    init(fetcher: UsageFetcher, settings: SettingsStore) {
        self.fetcher = fetcher
        self.settings = settings
        self.bindSettings()
        Task { await self.refresh() }
        self.startTimer()
    }

    func refresh() async {
        guard !self.isRefreshing else { return }
        self.isRefreshing = true
        defer { self.isRefreshing = false }

        do {
            let usage = try await self.fetcher.loadLatestUsage()
            self.snapshot = usage
            self.lastError = nil
        } catch {
            self.lastError = error.localizedDescription
        }
    }

    /// For demo/testing: drop the snapshot so the loading animation plays, then restore the last snapshot.
    func replayLoadingAnimation(duration: TimeInterval = 3) {
        guard !self.isRefreshing else { return }
        let current = self.snapshot
        self.snapshot = nil
        Task { @MainActor in
            try? await Task.sleep(for: .seconds(duration))
            self.snapshot = current
        }
    }

    private func bindSettings() {
        self.settings.$refreshFrequency
            .sink { [weak self] _ in
                self?.startTimer()
            }
            .store(in: &self.cancellables)
    }

    private func startTimer() {
        self.timerTask?.cancel()
        guard let wait = self.settings.refreshFrequency.seconds else { return }

        // Detached poller so the menu stays responsive while waiting.
        self.timerTask = Task.detached(priority: .utility) { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .seconds(wait))
                await self?.refresh()
            }
        }
    }

    deinit {
        self.timerTask?.cancel()
    }
}

// MARK: - UI

struct UsageRow: View {
    let title: String
    let window: RateWindow

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(self.title).font(.headline)
            let usageText = String(
                format: "%.0f%% left (%.0f%% used)",
                self.window.remainingPercent,
                self.window.usedPercent)
            Text(usageText)
            if let reset = window.resetsAt {
                Text("Resets \(reset.formatted(date: .abbreviated, time: .shortened))")
            }
        }
    }
}

struct MenuContent: View {
    @ObservedObject var store: UsageStore
    @ObservedObject var settings: SettingsStore
    let account: AccountInfo
    let updater: SPUStandardUpdaterController

    private var autoUpdateBinding: Binding<Bool> {
        Binding(
            get: { self.updater.updater.automaticallyChecksForUpdates },
            set: { self.updater.updater.automaticallyChecksForUpdates = $0 })
    }

    private var snapshot: UsageSnapshot? { self.store.snapshot }

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            if let snapshot {
                UsageRow(title: "5h limit", window: snapshot.primary)
                UsageRow(title: "Weekly limit", window: snapshot.secondary)
                Text("Updated \(snapshot.updatedAt.formatted(date: .omitted, time: .shortened))")
                    .foregroundStyle(.secondary)
            } else {
                Text("No usage yet").foregroundStyle(.secondary)
                if let error = store.lastError { Text(error).font(.caption) }
            }

            Divider()
            if let email = account.email {
                Text("Account: \(email)")
                    .foregroundStyle(.secondary)
            } else {
                Text("Account: unknown")
                    .foregroundStyle(.secondary)
            }
            if let plan = account.plan {
                Text("Plan: \(plan.capitalized)")
                    .foregroundStyle(.secondary)
            }

            Button {
                Task { await self.store.refresh() }
            } label: {
                Text(self.store.isRefreshing ? "Refreshing…" : "Refresh now")
            }
            .disabled(self.store.isRefreshing)
            .buttonStyle(.plain)
            Button("Usage Dashboard") {
                if let url = URL(string: "https://chatgpt.com/codex/settings/usage") {
                    NSWorkspace.shared.open(url)
                }
            }
            .buttonStyle(.plain)
            Divider()
            Menu("Settings") {
                Menu("Refresh every: \(self.settings.refreshFrequency.label)") {
                    ForEach(RefreshFrequency.allCases) { option in
                        Button {
                            self.settings.refreshFrequency = option
                        } label: {
                            if self.settings.refreshFrequency == option {
                                Label(option.label, systemImage: "checkmark")
                            } else {
                                Text(option.label)
                            }
                        }
                    }
                }
                Toggle("Automatically check for updates", isOn: self.autoUpdateBinding)
                Toggle("Launch at login", isOn: self.$settings.launchAtLogin)
                Button("Check for Updates…") {
                    self.updater.checkForUpdates(nil)
                }
                if self.settings.debugMenuEnabled {
                    Divider()
                    Button("Debug: Replay Loading Animation") {
                        NotificationCenter.default.post(name: .codexbarDebugReplayAllAnimations, object: nil)
                        self.store.replayLoadingAnimation()
                    }
                }
            }
            .buttonStyle(.plain)
            Button("About CodexBar") {
                showAbout()
            }
            .buttonStyle(.plain)
            Button("Quit") { NSApp.terminate(nil) }
                .buttonStyle(.plain)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
        .frame(minWidth: 240, alignment: .leading)
        .foregroundStyle(.primary)
        if self.settings.refreshFrequency == .manual {
            Text("Auto-refresh is off")
                .font(.caption2)
                .foregroundStyle(.secondary)
                .padding(.horizontal, 10)
        }
    }
}

@main
struct CodexBarApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var settings = SettingsStore()
    @StateObject private var store: UsageStore
    private let account: AccountInfo
    @State private var isInserted = true

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
            IconView(snapshot: self.store.snapshot, isStale: self.store.lastError != nil)
        }
        Settings {
            EmptyView()
        }
    }
}

struct IconView: View {
    let snapshot: UsageSnapshot?
    let isStale: Bool
    @State private var phase: CGFloat = 0
    @StateObject private var displayLink = DisplayLinkDriver()
    @State private var pattern: LoadingPattern = .knightRider
    @State private var debugCycle = false
    @State private var cycleIndex = 0
    @State private var cycleCounter = 0
    private let cycleIntervalTicks = 20
    private let patterns = LoadingPattern.allCases

    var body: some View {
        Group {
            if let snapshot {
                Image(nsImage: IconRenderer.makeIcon(
                    primaryRemaining: snapshot.primary.remainingPercent,
                    weeklyRemaining: snapshot.secondary.remainingPercent,
                    stale: self.isStale))
            } else {
                Image(nsImage: IconRenderer.makeIcon(
                    primaryRemaining: self.loadingPrimary,
                    weeklyRemaining: self.loadingSecondary,
                    stale: false))
                .onReceive(self.displayLink.$tick) { _ in
                    self.phase += 0.18 // a bit slower
                    if self.debugCycle {
                        self.cycleCounter += 1
                        if self.cycleCounter >= self.cycleIntervalTicks {
                            self.cycleCounter = 0
                            self.cycleIndex = (self.cycleIndex + 1) % self.patterns.count
                            self.pattern = self.patterns[self.cycleIndex]
                        }
                    }
                }
            }
        }
        .onAppear {
            self.displayLink.start(fps: 20)
            self.pattern = self.patterns.randomElement() ?? .knightRider
        }
        .onDisappear {
            self.displayLink.stop()
        }
        .onChange(of: self.snapshot == nil, initial: false) { _, isLoading in
            guard isLoading else {
                self.debugCycle = false
                return
            }
            if !self.debugCycle {
                self.pattern = self.patterns.randomElement() ?? .knightRider
            }
        }
        .onReceive(NotificationCenter.default.publisher(for: .codexbarDebugReplayAllAnimations)) { _ in
            self.debugCycle = true
            self.cycleIndex = 0
            self.cycleCounter = 0
            self.pattern = self.patterns.first ?? .knightRider
        }
    }

    private var loadingPrimary: Double {
        self.pattern.value(phase: Double(self.phase))
    }

    private var loadingSecondary: Double {
        self.pattern.value(phase: Double(self.phase + self.pattern.secondaryOffset))
    }
}


final class AppDelegate: NSObject, NSApplicationDelegate {
    let updaterController = SPUStandardUpdaterController(
        startingUpdater: true,
        updaterDelegate: nil,
        userDriverDelegate: nil)
}

@MainActor
private func showAbout() {
    NSApp.activate(ignoringOtherApps: true)

    let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "–"
    let build = Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? ""
    let versionString = build.isEmpty ? version : "\(version) (\(build))"

    let separator: NSAttributedString = NSAttributedString(string: " · ", attributes: [
        .font: NSFont.systemFont(ofSize: NSFont.smallSystemFontSize),
    ])

    func makeLink(_ title: String, urlString: String) -> NSAttributedString {
        NSAttributedString(string: title, attributes: [
            .link: URL(string: urlString) as Any,
            .font: NSFont.systemFont(ofSize: NSFont.smallSystemFontSize),
        ])
    }

    let credits = NSMutableAttributedString(string: "Peter Steinberger — MIT License\n")
    credits.append(makeLink("GitHub", urlString: "https://github.com/steipete/CodexBar"))
    credits.append(separator)
    credits.append(makeLink("Website", urlString: "https://steipete.me"))
    credits.append(separator)
    credits.append(makeLink("Twitter", urlString: "https://twitter.com/steipete"))
    credits.append(separator)
    credits.append(makeLink("Email", urlString: "mailto:peter@steipete.me"))

    let options: [NSApplication.AboutPanelOptionKey: Any] = [
        .applicationName: "CodexBar",
        .applicationVersion: versionString,
        .version: versionString,
        .credits: credits,
        // Use bundled icon if available; fallback to empty image to avoid nil coercion warnings.
        .applicationIcon: (NSApplication.shared.applicationIconImage ?? NSImage()) as Any,
    ]

    NSApp.orderFrontStandardAboutPanel(options: options)
}

enum LaunchAtLoginManager {
    @MainActor
    static func setEnabled(_ enabled: Bool) {
        guard #available(macOS 13, *) else { return }
        let service = SMAppService.mainApp
        if enabled {
            // Idempotent; safe to call repeatedly.
            try? service.register()
        } else {
            try? service.unregister()
        }
    }
}
