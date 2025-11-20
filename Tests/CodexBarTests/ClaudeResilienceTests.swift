import Testing
@testable import CodexBar

@Suite
struct ClaudeResilienceTests {
    @Test
    func suppressesSingleFlakeWhenPriorDataExists() {
        var gate = ConsecutiveFailureGate()
        let firstFailure = gate.shouldSurfaceError(onFailureWithPriorData: true)
        let secondFailure = gate.shouldSurfaceError(onFailureWithPriorData: true)
        #expect(firstFailure == false)
        #expect(secondFailure == true)
    }

    @Test
    func surfacesFailureWithoutPriorData() {
        var gate = ConsecutiveFailureGate()
        let shouldSurface = gate.shouldSurfaceError(onFailureWithPriorData: false)
        #expect(shouldSurface)
    }

    @Test
    func resetsAfterSuccess() {
        var gate = ConsecutiveFailureGate()
        _ = gate.shouldSurfaceError(onFailureWithPriorData: true)
        gate.recordSuccess()
        let shouldSurface = gate.shouldSurfaceError(onFailureWithPriorData: true)
        #expect(shouldSurface == false)
    }
}
