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
            let usage = try self.fetcher.loadLatestUsage()
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
            Text(title).font(.headline)
            Text("\(window.remainingPercent, specifier: "%.0f")% left (\(window.usedPercent, specifier: "%.0f")% used)")
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
                    .foregroundColor(Color(nsColor: .secondaryLabelColor))
            } else {
                Text("No usage yet").foregroundStyle(.secondary)
                if let error = store.lastError { Text(error).font(.caption) }
            }

            Divider()
            if let email = account.email {
                Text("Account: \(email)")
                    .foregroundColor(Color(nsColor: .secondaryLabelColor))
            } else {
                Text("Account: unknown")
                    .foregroundColor(Color(nsColor: .secondaryLabelColor))
            }
            if let plan = account.plan {
                Text("Plan: \(plan.capitalized)")
                    .foregroundColor(Color(nsColor: .secondaryLabelColor))
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
            Button {
                Task { await store.refresh() }
            } label: {
                Label(store.isRefreshing ? "Refreshing…" : "Refresh now", systemImage: "arrow.clockwise")
            }
            Divider()
            Button("About CodexBar") {
                showAbout()
            }
            Button("View on GitHub") {
                if let url = URL(string: "https://github.com/steipete/CodexBar") {
                    NSWorkspace.shared.open(url)
                }
            }
            Button("Quit") { NSApp.terminate(nil) }
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
        .frame(minWidth: 240, alignment: .leading)
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

    let alert = NSAlert()
    alert.alertStyle = .informational
    alert.messageText = "CodexBar 0.1.0"
    alert.informativeText = "Peter Steinberger — MIT License\nhttps://github.com/steipete/CodexBar"
    alert.icon = NSApplication.shared.applicationIconImage
    alert.addButton(withTitle: "OK")
    alert.runModal()
}
