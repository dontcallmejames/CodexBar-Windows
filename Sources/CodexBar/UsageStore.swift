import Combine
import Foundation

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
