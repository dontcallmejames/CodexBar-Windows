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
}
