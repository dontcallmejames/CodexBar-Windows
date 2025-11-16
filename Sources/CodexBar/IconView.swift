import SwiftUI

@MainActor
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
                        self.phase += 0.18
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
