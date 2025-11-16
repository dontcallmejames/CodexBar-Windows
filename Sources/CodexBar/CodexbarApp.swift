import AppKit
import Combine
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

    init(userDefaults: UserDefaults = .standard) {
        let raw = userDefaults.string(forKey: "refreshFrequency") ?? RefreshFrequency.twoMinutes.rawValue
        self.refreshFrequency = RefreshFrequency(rawValue: raw) ?? .twoMinutes
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
            Text(
                "\(self.window.remainingPercent, specifier: "%.0f")% left (\(self.window.usedPercent, specifier: "%.0f")% used)")
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

            Divider()
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
            .buttonStyle(.plain)
            Button {
                Task { await self.store.refresh() }
            } label: {
                Label(self.store.isRefreshing ? "Refreshing…" : "Refresh now", systemImage: "arrow.clockwise")
            }
            .disabled(self.store.isRefreshing)
            .buttonStyle(.plain)
            Divider()
            Button("About CodexBar") {
                showAbout()
            }
            .buttonStyle(.plain)
            Button("View on GitHub") {
                if let url = URL(string: "https://github.com/steipete/CodexBar") {
                    NSWorkspace.shared.open(url)
                }
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
            MenuContent(store: self.store, settings: self.settings, account: self.account)
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

    var body: some View {
        if let snapshot {
            Image(nsImage: IconRenderer.makeIcon(
                primaryRemaining: snapshot.primary.remainingPercent,
                weeklyRemaining: snapshot.secondary.remainingPercent,
                stale: self.isStale))
        } else {
            Image(systemName: "chart.bar.fill")
        }
    }
}

@MainActor
private func showAbout() {
    NSApp.activate(ignoringOtherApps: true)

    let credits = NSAttributedString(
        string: "Peter Steinberger — MIT License\nhttps://github.com/steipete/CodexBar",
        attributes: [
            .font: NSFont.systemFont(ofSize: NSFont.smallSystemFontSize),
            .foregroundColor: NSColor.secondaryLabelColor,
        ])

    let options: [NSApplication.AboutPanelOptionKey: Any] = [
        .applicationName: "CodexBar 📊",
        .applicationVersion: "0.1.0",
        .version: "0.1.0",
        .credits: credits,
        // Use bundled icon if available; fallback to empty image to avoid nil coercion warnings.
        .applicationIcon: (NSApplication.shared.applicationIconImage ?? NSImage()) as Any,
    ]

    NSApp.orderFrontStandardAboutPanel(options: options)
}
