import Foundation
import Testing
@testable import CodexBar

@Suite
struct StatusProbeTests {
    @Test
    func parseCodexStatus() throws {
        let sample = """
        Model: gpt
        Credits: 980 credits
        5h limit: [#####] 75% left
        Weekly limit: [##] 25% left
        """
        let snap = try CodexStatusProbe.parse(text: sample)
        #expect(snap.credits == 980)
        #expect(snap.fiveHourPercentLeft == 75)
        #expect(snap.weeklyPercentLeft == 25)
    }

    @Test
    func parseCodexStatusWithAnsiAndResets() throws {
        let sample = """
        \u{001B}[38;5;245mCredits:\u{001B}[0m 557 credits
        5h limit: [█████     ] 50% left (resets 09:01)
        Weekly limit: [███████   ] 85% left (resets 04:01 on 27 Nov)
        """
        let snap = try CodexStatusProbe.parse(text: sample)
        #expect(snap.credits == 557)
        #expect(snap.fiveHourPercentLeft == 50)
        #expect(snap.weeklyPercentLeft == 85)
    }

    @Test
    func parseClaudeStatus() throws {
        let sample = """
        Current session
        12% used  (Resets 11am)
        Current week (all models)
        55% used  (Resets Nov 21)
        Current week (Opus)
        5% used (Resets Nov 21)
        Account: user@example.com
        Org: Example Org
        """
        let snap = try ClaudeStatusProbe.parse(text: sample)
        #expect(snap.sessionPercentLeft == 88)
        #expect(snap.weeklyPercentLeft == 45)
        #expect(snap.opusPercentLeft == 95)
        #expect(snap.accountEmail == "user@example.com")
        #expect(snap.accountOrganization == "Example Org")
        #expect(snap.primaryResetDescription == "Resets 11am")
        #expect(snap.secondaryResetDescription == "Resets Nov 21")
        #expect(snap.opusResetDescription == "Resets Nov 21")
    }

    @Test
    func parseClaudeStatusWithANSI() throws {
        let sample = """
        \u{001B}[35mCurrent session\u{001B}[0m
        40% used  (Resets 11am)
        Current week (all models)
        10% used  (Resets Nov 27)
        Current week (Opus)
        0% used (Resets Nov 27)
        Account: user@example.com
        Org: ACME
        \u{001B}[0m
        """
        let snap = try ClaudeStatusProbe.parse(text: sample)
        #expect(snap.sessionPercentLeft == 60)
        #expect(snap.weeklyPercentLeft == 90)
        #expect(snap.opusPercentLeft == 100)
        #expect(snap.primaryResetDescription == "Resets 11am")
        #expect(snap.secondaryResetDescription == "Resets Nov 27")
        #expect(snap.opusResetDescription == "Resets Nov 27")
    }

    @Test
    func surfacesClaudeTokenExpired() {
        let sample = """
        Settings:  Status   Config   Usage

        Error: Failed to load usage data: {"type":"error","error":{"type":"authentication_error",
        "message":"OAuth token has expired. Please obtain a new token or refresh your existing token.",
        "details":{"error_visibility":"user_facing","error_code":"token_expired"}},\
        "request_id":"req_123"}
        """

        do {
            _ = try ClaudeStatusProbe.parse(text: sample)
            #expect(Bool(false), "Parsing should fail for auth error")
        } catch let ClaudeStatusProbeError.parseFailed(message) {
            let lower = message.lowercased()
            #expect(lower.contains("token"))
            #expect(lower.contains("login"))
        } catch {
            #expect(Bool(false), "Unexpected error: \(error)")
        }
    }

    @Test
    func liveCodexStatus() async throws {
        guard ProcessInfo.processInfo.environment["LIVE_CODEX_STATUS"] == "1" else { return }

        let probe = CodexStatusProbe()
        do {
            let snap = try await probe.fetch()
            let summary = """
            Live Codex status:
            \(snap.rawText)
            values: 5h \(snap.fiveHourPercentLeft ?? -1)% left,
            weekly \(snap.weeklyPercentLeft ?? -1)% left,
            credits \(snap.credits ?? -1)
            """
            print(summary)
        } catch {
            // Dump raw PTY text to help debug.
            let runner = TTYCommandRunner()
            let res = try runner.run(
                binary: "codex",
                send: "/status\n",
                options: .init(rows: 60, cols: 200, timeout: 12))
            print("RAW CODEX PTY OUTPUT BEGIN\n\(res.text)\nRAW CODEX PTY OUTPUT END")
            let clean = TextParsing.stripANSICodes(res.text)
            print("CLEAN CODEX OUTPUT BEGIN\n\(clean)\nCLEAN CODEX OUTPUT END")
            let five = TextParsing.firstInt(pattern: #"5h limit[^\n]*?([0-9]{1,3})%\s+left"#, text: clean) ?? -1
            let week = TextParsing.firstInt(pattern: #"Weekly limit[^\n]*?([0-9]{1,3})%\s+left"#, text: clean) ?? -1
            let credits = TextParsing.firstNumber(pattern: #"Credits:\s*([0-9][0-9.,]*)"#, text: clean) ?? -1
            print("Parsed probes => 5h \(five)% weekly \(week)% credits \(credits)")
            throw error
        }
    }
}
