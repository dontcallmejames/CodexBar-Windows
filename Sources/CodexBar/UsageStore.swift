import AppKit
import Combine
import Foundation

enum IconStyle {
    case codex
    case claude
    case combined
}

enum UsageProvider: CaseIterable {
    case codex
    case claude
}

/// Tracks consecutive failures so we can ignore a single flake when we previously had fresh data.
struct ConsecutiveFailureGate {
    private(set) var streak: Int = 0

    mutating func recordSuccess() {
        self.streak = 0
    }

    mutating func reset() {
        self.streak = 0
    }

    /// Returns true when the caller should surface the error to the UI.
    mutating func shouldSurfaceError(onFailureWithPriorData hadPriorData: Bool) -> Bool {
        self.streak += 1
        if hadPriorData, self.streak == 1 { return false }
        return true
    }
}

@MainActor
final class UsageStore: ObservableObject {
    @Published private var snapshots: [UsageProvider: UsageSnapshot] = [:]
    @Published private var errors: [UsageProvider: String] = [:]
    @Published var credits: CreditsSnapshot?
    @Published var lastCreditsError: String?
    @Published var codexVersion: String?
    @Published var claudeVersion: String?
    @Published var claudeAccountEmail: String?
    @Published var claudeAccountOrganization: String?
    @Published var isRefreshing = false
    @Published var debugForceAnimation = false

    private struct ProviderSpec {
        let style: IconStyle
        let isEnabled: () -> Bool
        let fetch: () async throws -> UsageSnapshot
        let onSuccess: ((UsageSnapshot) -> Void)?
    }

    private let codexFetcher: UsageFetcher
    private let claudeFetcher: any ClaudeUsageFetching
    private let settings: SettingsStore
    private var failureGates: [UsageProvider: ConsecutiveFailureGate] = [:]
    private var providerSpecs: [UsageProvider: ProviderSpec] = [:]
    private var timerTask: Task<Void, Never>?
    private var cancellables = Set<AnyCancellable>()

    init(
        fetcher: UsageFetcher,
        claudeFetcher: any ClaudeUsageFetching = ClaudeUsageFetcher(),
        settings: SettingsStore)
    {
        self.codexFetcher = fetcher
        self.claudeFetcher = claudeFetcher
        self.settings = settings
        self
            .failureGates = Dictionary(uniqueKeysWithValues: UsageProvider.allCases
                .map { ($0, ConsecutiveFailureGate()) })
        self.providerSpecs = Self.makeProviderSpecs(
            settings: settings,
            codexFetcher: fetcher,
            claudeFetcher: claudeFetcher,
            onClaudeSuccess: { [weak self] snap in
                self?.claudeAccountEmail = snap.accountEmail
                self?.claudeAccountOrganization = snap.accountOrganization
            })
        self.bindSettings()
        self.detectVersions()
        Task { await self.refresh() }
        self.startTimer()
    }

    var codexSnapshot: UsageSnapshot? { self.snapshots[.codex] }
    var claudeSnapshot: UsageSnapshot? { self.snapshots[.claude] }
    var lastCodexError: String? { self.errors[.codex] }
    var lastClaudeError: String? { self.errors[.claude] }
    func error(for provider: UsageProvider) -> String? { self.errors[provider] }

    var preferredSnapshot: UsageSnapshot? {
        if self.settings.showCodexUsage, let codexSnapshot {
            return codexSnapshot
        }
        if self.settings.showClaudeUsage, let claudeSnapshot {
            return claudeSnapshot
        }
        return nil
    }

    var iconStyle: IconStyle {
        self.settings.showClaudeUsage ? .claude : .codex
    }

    var isStale: Bool {
        (self.settings.showCodexUsage && self.lastCodexError != nil) ||
            (self.settings.showClaudeUsage && self.lastClaudeError != nil)
    }

    func enabledProviders() -> [UsageProvider] {
        var result: [UsageProvider] = []
        if self.settings.showCodexUsage { result.append(.codex) }
        if self.settings.showClaudeUsage { result.append(.claude) }
        return result
    }

    func snapshot(for provider: UsageProvider) -> UsageSnapshot? {
        self.snapshots[provider]
    }

    func style(for provider: UsageProvider) -> IconStyle {
        self.providerSpecs[provider]?.style ?? .codex
    }

    func isStale(provider: UsageProvider) -> Bool {
        self.errors[provider] != nil
    }

    func refresh() async {
        guard !self.isRefreshing else { return }
        self.isRefreshing = true
        defer { self.isRefreshing = false }

        await withTaskGroup(of: Void.self) { group in
            for provider in UsageProvider.allCases {
                group.addTask { await self.refreshProvider(provider) }
            }
            group.addTask { await self.refreshCreditsIfNeeded() }
        }
    }

    /// For demo/testing: drop the snapshot so the loading animation plays, then restore the last snapshot.
    func replayLoadingAnimation(duration: TimeInterval = 3) {
        let current = self.preferredSnapshot
        self.snapshots.removeAll()
        self.debugForceAnimation = true
        Task { @MainActor in
            try? await Task.sleep(for: .seconds(duration))
            if let current {
                if self.settings.showCodexUsage {
                    self.snapshots[.codex] = current
                } else if self.settings.showClaudeUsage {
                    self.snapshots[.claude] = current
                }
            }
            self.debugForceAnimation = false
        }
    }

    // MARK: - Private

    private func bindSettings() {
        self.settings.$refreshFrequency
            .sink { [weak self] _ in
                self?.startTimer()
            }
            .store(in: &self.cancellables)

        self.settings.objectWillChange
            .sink { [weak self] _ in
                Task { await self?.refresh() }
            }
            .store(in: &self.cancellables)
    }

    private func startTimer() {
        self.timerTask?.cancel()
        guard let wait = self.settings.refreshFrequency.seconds else { return }

        // Background poller so the menu stays responsive; canceled when settings change or store deallocates.
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

    private func refreshProvider(_ provider: UsageProvider) async {
        guard let spec = self.providerSpecs[provider] else { return }

        if !spec.isEnabled() {
            await MainActor.run {
                self.snapshots.removeValue(forKey: provider)
                self.errors[provider] = nil
                self.failureGates[provider]?.reset()
            }
            return
        }

        do {
            let snapshot = try await spec.fetch()
            await MainActor.run {
                self.snapshots[provider] = snapshot
                self.errors[provider] = nil
                self.failureGates[provider]?.recordSuccess()
                spec.onSuccess?(snapshot)
            }
        } catch {
            await MainActor.run {
                let hadPriorData = self.snapshots[provider] != nil
                let shouldSurface = self.failureGates[provider]?
                    .shouldSurfaceError(onFailureWithPriorData: hadPriorData) ?? true
                if shouldSurface {
                    self.errors[provider] = error.localizedDescription
                    self.snapshots.removeValue(forKey: provider)
                } else {
                    self.errors[provider] = nil
                }
            }
        }
    }

    private func refreshCreditsIfNeeded() async {
        guard self.settings.showCodexUsage else { return }
        do {
            let snap = try await CodexStatusProbe().fetch()
            let credits = CreditsSnapshot(remaining: snap.credits ?? 0, events: [], updatedAt: Date())
            await MainActor.run {
                self.credits = credits
                self.lastCreditsError = nil
            }
        } catch {
            await MainActor.run {
                self.lastCreditsError = error.localizedDescription
                self.credits = nil
            }
        }
    }

    func debugDumpClaude() async {
        let output = await self.claudeFetcher.debugRawProbe(model: "sonnet")
        let url = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("codexbar-claude-probe.txt")
        try? output.write(to: url, atomically: true, encoding: String.Encoding.utf8)
        await MainActor.run {
            let snippet = String(output.prefix(180)).replacingOccurrences(of: "\n", with: " ")
            self.errors[.claude] = "[Claude] \(snippet) (saved: \(url.path))"
            NSWorkspace.shared.open(url)
        }
    }

    private static func makeProviderSpecs(
        settings: SettingsStore,
        codexFetcher: UsageFetcher,
        claudeFetcher: any ClaudeUsageFetching,
        onClaudeSuccess: @escaping (UsageSnapshot) -> Void) -> [UsageProvider: ProviderSpec]
    {
        let codexSpec = ProviderSpec(
            style: .codex,
            isEnabled: { settings.showCodexUsage },
            fetch: { try await codexFetcher.loadLatestUsage() },
            onSuccess: nil)

        let claudeSpec = ProviderSpec(
            style: .claude,
            isEnabled: { settings.showClaudeUsage },
            fetch: {
                let usage = try await claudeFetcher.loadLatestUsage(model: "sonnet")
                return UsageSnapshot(
                    primary: usage.primary,
                    secondary: usage.secondary,
                    tertiary: usage.opus,
                    updatedAt: usage.updatedAt,
                    accountEmail: usage.accountEmail,
                    accountOrganization: usage.accountOrganization,
                    loginMethod: usage.loginMethod)
            },
            onSuccess: onClaudeSuccess)

        return [.codex: codexSpec, .claude: claudeSpec]
    }

    private func detectVersions() {
        Task.detached { [claudeFetcher] in
            let codexVer = Self.readCLI("codex", args: ["--version"])
            let claudeVer = claudeFetcher.detectVersion()
            await MainActor.run {
                self.codexVersion = codexVer
                self.claudeVersion = claudeVer
            }
        }
    }

    private nonisolated static func readCLI(_ cmd: String, args: [String]) -> String? {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/env")
        process.arguments = [cmd] + args
        let pipe = Pipe()
        process.standardOutput = pipe
        try? process.run()
        process.waitUntilExit()
        guard process.terminationStatus == 0 else { return nil }
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        let text = String(decoding: data, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines)
        return text.isEmpty ? nil : text
    }
}
