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
        } catch ClaudeStatusProbeError.parseFailed(let message) {
            let lower = message.lowercased()
            #expect(lower.contains("token"))
            #expect(lower.contains("login"))
        } catch {
            #expect(Bool(false), "Unexpected error: \(error)")
        }
    }
}
