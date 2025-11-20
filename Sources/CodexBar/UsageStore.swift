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
    @Published var codexSnapshot: UsageSnapshot?
    @Published var claudeSnapshot: UsageSnapshot?
    @Published var credits: CreditsSnapshot?
    @Published var lastCodexError: String?
    @Published var lastClaudeError: String?
    @Published var lastCreditsError: String?
    @Published var codexVersion: String?
    @Published var claudeVersion: String?
    @Published var claudeAccountEmail: String?
    @Published var claudeAccountOrganization: String?
    @Published var isRefreshing = false
    @Published var debugForceAnimation = false

    private let codexFetcher: UsageFetcher
    private let claudeFetcher: any ClaudeUsageFetching
    private let settings: SettingsStore
    private var claudeFailureGate = ConsecutiveFailureGate()
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
        self.bindSettings()
        self.detectVersions()
        Task { await self.refresh() }
        self.startTimer()
    }

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
        switch provider {
        case .codex: self.codexSnapshot
        case .claude: self.claudeSnapshot
        }
    }

    func style(for provider: UsageProvider) -> IconStyle {
        switch provider {
        case .codex: .codex
        case .claude: .claude
        }
    }

    func isStale(provider: UsageProvider) -> Bool {
        switch provider {
        case .codex: self.lastCodexError != nil
        case .claude: self.lastClaudeError != nil
        }
    }

    func refresh() async {
        guard !self.isRefreshing else { return }
        self.isRefreshing = true
        defer { self.isRefreshing = false }

        async let codexTask: Void = self.refreshCodexIfNeeded()
        async let claudeTask: Void = self.refreshClaudeIfNeeded()
        async let creditsTask: Void = self.refreshCreditsIfNeeded()
        _ = await (codexTask, claudeTask, creditsTask)
    }

    /// For demo/testing: drop the snapshot so the loading animation plays, then restore the last snapshot.
    func replayLoadingAnimation(duration: TimeInterval = 3) {
        let current = self.preferredSnapshot
        self.codexSnapshot = nil
        self.claudeSnapshot = nil
        self.debugForceAnimation = true
        Task { @MainActor in
            try? await Task.sleep(for: .seconds(duration))
            if let current {
                if self.settings.showCodexUsage {
                    self.codexSnapshot = current
                } else if self.settings.showClaudeUsage {
                    self.claudeSnapshot = current
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

    private func refreshCodexIfNeeded() async {
        guard self.settings.showCodexUsage else {
            self.codexSnapshot = nil
            self.lastCodexError = nil
            return
        }

        do {
            let usage = try await self.codexFetcher.loadLatestUsage()
            await MainActor.run {
                self.codexSnapshot = usage
                self.lastCodexError = nil
            }
        } catch {
            await MainActor.run {
                self.lastCodexError = error.localizedDescription
                self.codexSnapshot = nil
            }
        }
    }

    private func refreshClaudeIfNeeded() async {
        guard self.settings.showClaudeUsage else {
            self.claudeSnapshot = nil
            self.lastClaudeError = nil
            self.claudeFailureGate.reset()
            return
        }

        do {
            let usage = try await self.fetchClaudeWithRetry()
            await MainActor.run {
                let snapshot = UsageSnapshot(
                    primary: usage.primary,
                    secondary: usage.secondary,
                    tertiary: usage.opus,
                    updatedAt: usage.updatedAt,
                    accountEmail: usage.accountEmail,
                    accountOrganization: usage.accountOrganization,
                    loginMethod: usage.loginMethod)
                self.claudeSnapshot = snapshot
                self.claudeAccountEmail = usage.accountEmail
                self.claudeAccountOrganization = usage.accountOrganization
                self.lastClaudeError = nil
                self.claudeFailureGate.recordSuccess()
            }
        } catch {
            await MainActor.run {
                let hadPriorData = self.claudeSnapshot != nil
                let shouldSurface = self.claudeFailureGate.shouldSurfaceError(onFailureWithPriorData: hadPriorData)
                if shouldSurface {
                    self.lastClaudeError = error.localizedDescription
                    self.claudeSnapshot = nil
                } else {
                    // Keep showing the last good snapshot and suppress the single flake.
                    self.lastClaudeError = nil
                }
            }
        }
    }

    private func fetchClaudeWithRetry() async throws -> ClaudeUsageSnapshot {
        do {
            return try await self.claudeFetcher.loadLatestUsage(model: "sonnet")
        } catch {
            // Retry once to ride out slow renders or dropped keystrokes.
            return try await self.claudeFetcher.loadLatestUsage(model: "sonnet")
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
            self.lastClaudeError = "[Claude] \(snippet) (saved: \(url.path))"
            NSWorkspace.shared.open(url)
        }
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
