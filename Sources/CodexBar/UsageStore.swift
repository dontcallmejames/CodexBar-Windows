import Combine
import Foundation
import WebKit

@MainActor
final class UsageStore: ObservableObject {
    @Published var snapshot: UsageSnapshot?
    @Published var credits: CreditsSnapshot?
    @Published var lastError: String?
    @Published var lastCreditsError: String?
    @Published var isRefreshing = false

    private let fetcher: UsageFetcher
    private let creditsFetcher: CreditsFetcher
    private let settings: SettingsStore
    private var timerTask: Task<Void, Never>?
    private var cancellables = Set<AnyCancellable>()

    init(fetcher: UsageFetcher, creditsFetcher: CreditsFetcher = .init(), settings: SettingsStore) {
        self.fetcher = fetcher
        self.creditsFetcher = creditsFetcher
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
            async let usageTask = self.fetcher.loadLatestUsage()
            async let creditsTask: Void = self.fetchCredits()

            let (usage, _) = try await (usageTask, creditsTask)

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

    private func fetchCredits() async {
        do {
            let credits = try await self.creditsFetcher.loadLatestCredits(debugDump: self.settings.creditsDebugDump)
            self.credits = credits
            self.lastCreditsError = nil
        } catch {
            self.lastCreditsError = error.localizedDescription
        }
    }

    func clearCookies() async {
        let dataStore = WKWebsiteDataStore.default()
        let types = WKWebsiteDataStore.allWebsiteDataTypes()
        await dataStore.removeData(ofTypes: types, modifiedSince: Date.distantPast)
        await MainActor.run {
            self.snapshot = nil
            self.credits = nil
            self.lastCreditsError = "Cleared cookies; sign in again to fetch credits."
        }
    }
}
