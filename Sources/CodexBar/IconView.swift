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
    private let loadingFPS: Double = 12
    // Advance to next pattern every N ticks when debug cycling.
    private let cycleIntervalTicks = 20
    private let patterns = LoadingPattern.allCases

    private var isLoading: Bool { self.snapshot == nil }

    var body: some View {
        Group {
            if let snapshot {
                Image(nsImage: IconRenderer.makeIcon(
                    primaryRemaining: snapshot.primary.remainingPercent,
                    weeklyRemaining: snapshot.secondary.remainingPercent,
                    stale: self.isStale))
            } else {
                // Loading: animate bars with the current pattern until data arrives.
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
        .onChange(of: self.isLoading, initial: true) { _, isLoading in
            if isLoading {
                self.displayLink.start(fps: self.loadingFPS)
                if !self.debugCycle {
                    self.pattern = self.patterns.randomElement() ?? .knightRider
                }
            } else {
                self.displayLink.stop()
                self.debugCycle = false
                self.phase = 0
            }
        }
        .onDisappear { self.displayLink.stop() }
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
