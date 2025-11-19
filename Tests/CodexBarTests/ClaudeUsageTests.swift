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

    @Test
    func parsesOpusAndAccount() {
        let json = """
        {
          "ok": true,
          "session_5h": { "pct_used": 2, "resets": "10:59pm (Europe/Vienna)" },
          "week_all_models": { "pct_used": 13, "resets": "Nov 21 at 4:59am (Europe/Vienna)" },
          "week_opus": { "pct_used": 0, "resets": "" },
          "account_email": " steipete@gmail.com ",
          "account_org": ""
        }
        """
        let data = Data(json.utf8)
        let snap = ClaudeUsageFetcher.parse(json: data)
        #expect(snap?.opus?.usedPercent == 0)
        #expect(snap?.opus?.resetDescription == "")
        #expect(snap?.accountEmail == "steipete@gmail.com")
        #expect(snap?.accountOrganization == nil)
    }

    @Test
    func trimsAccountFields() {
        let cases: [[String: String?]] = [
            ["email": " steipete@gmail.com ", "org": "  Org  "],
            ["email": "", "org": " Claude Max Account "],
            ["email": nil, "org": " "]
        ]

        for entry in cases {
            var payload = [
                "ok": true,
                "session_5h": ["pct_used": 0, "resets": ""],
                "week_all_models": ["pct_used": 0, "resets": ""]
            ] as [String: Any]
            if let email = entry["email"] ?? nil { payload["account_email"] = email }
            if let org = entry["org"] ?? nil { payload["account_org"] = org }
            let data = try! JSONSerialization.data(withJSONObject: payload)
            let snap = ClaudeUsageFetcher.parse(json: data)
            let expectedEmail = (entry["email"] ?? nil)?.trimmingCharacters(in: .whitespacesAndNewlines)
            let normalizedEmail = (expectedEmail?.isEmpty ?? true) ? nil : expectedEmail
            #expect(snap?.accountEmail == normalizedEmail)
            let expectedOrg = (entry["org"] ?? nil)?.trimmingCharacters(in: .whitespacesAndNewlines)
            let normalizedOrg = (expectedOrg?.isEmpty ?? true) ? nil : expectedOrg
            #expect(snap?.accountOrganization == normalizedOrg)
        }
    }
}
