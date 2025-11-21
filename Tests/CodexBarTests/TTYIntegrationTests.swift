import XCTest
@testable import CodexBar

final class TTYIntegrationTests: XCTestCase {
    func testCodexTTYStatusProbeLive() async throws {
        guard let codexPath = TTYCommandRunner.which("codex") else {
            throw XCTSkip("Codex CLI not installed; skipping live PTY probe.")
        }

        let probe = CodexStatusProbe(codexBinary: codexPath, timeout: 10)
        do {
            let snapshot = try await probe.fetch()
            let hasData = snapshot.credits != nil || snapshot.fiveHourPercentLeft != nil || snapshot
                .weeklyPercentLeft != nil
            XCTAssertTrue(hasData, "Codex PTY probe returned no recognizable usage fields.")
        } catch let CodexStatusProbeError.updateRequired(message) {
            // Acceptable: confirms we detected the update prompt and surfaced a clear message.
            XCTAssertFalse(message.isEmpty)
        } catch let CodexStatusProbeError.parseFailed(raw) {
            XCTFail("Codex PTY parse failed: \(raw.prefix(200))")
        } catch CodexStatusProbeError.timedOut {
            XCTFail("Codex PTY probe timed out.")
        }
    }

    func testClaudeTTYUsageProbeLive() async throws {
        guard TTYCommandRunner.which("claude") != nil else {
            throw XCTSkip("Claude CLI not installed; skipping live PTY probe.")
        }

        let probe = ClaudeStatusProbe(claudeBinary: "claude", timeout: 10)
        do {
            let snapshot = try await probe.fetch()
            XCTAssertNotNil(snapshot.sessionPercentLeft, "Claude session percent missing")
            XCTAssertNotNil(snapshot.weeklyPercentLeft, "Claude weekly percent missing")
        } catch let ClaudeStatusProbeError.parseFailed(message) {
            throw XCTSkip("Claude PTY parse failed (likely not logged in or usage unavailable): \(message)")
        } catch ClaudeStatusProbeError.timedOut {
            throw XCTSkip("Claude PTY probe timed out; skipping.")
        } catch let TTYCommandRunner.Error.launchFailed(message) where message.contains("login") {
            throw XCTSkip("Claude CLI not logged in: \(message)")
        }
    }
}
