import Foundation
import Testing
@testable import CodexBar

@Suite
struct ClaudeUsageTests {
    @Test
    func parsesUsageJSON() {
        let json = """
        {
          "ok": true,
          "session_5h": { "pct_used": 1, "resets": "11am (Europe/Vienna)" },
          "week_all_models": { "pct_used": 8, "resets": "Nov 21 at 5am (Europe/Vienna)" },
          "week_opus": { "pct_used": 0, "resets": "Nov 21 at 5am (Europe/Vienna)" }
        }
        """
        let data = Data(json.utf8)
        let snap = ClaudeUsageFetcher.parse(json: data)
        #expect(snap != nil)
        #expect(snap?.primary.usedPercent == 1)
        #expect(snap?.secondary.usedPercent == 8)
        #expect(snap?.primary.resetDescription == "11am (Europe/Vienna)")
    }
}
