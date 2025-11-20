import Foundation

struct ClaudeStatusSnapshot {
    let sessionPercentLeft: Int?
    let weeklyPercentLeft: Int?
    let opusPercentLeft: Int?
    let accountEmail: String?
    let accountOrganization: String?
    let rawText: String
}

enum ClaudeStatusProbeError: LocalizedError {
    case claudeNotInstalled
    case parseFailed(String)
    case timedOut

    var errorDescription: String? {
        switch self {
        case .claudeNotInstalled:
            "Claude CLI is not installed or not on PATH."
        case .parseFailed(let msg):
            "Could not parse Claude usage: \(msg)"
        case .timedOut:
            "Claude usage probe timed out."
        }
    }
}

/// Runs `claude` inside a PTY, sends `/usage`, and parses the rendered text panel.
struct ClaudeStatusProbe {
    var claudeBinary: String = "claude"
    var timeout: TimeInterval = 10.0

    func fetch() async throws -> ClaudeStatusSnapshot {
        guard TTYCommandRunner.which(claudeBinary) != nil else { throw ClaudeStatusProbeError.claudeNotInstalled }
        let runner = TTYCommandRunner()
        let result = try runner.run(binary: claudeBinary, send: "/usage\n", options: .init(rows: 50, cols: 160, timeout: timeout))
        return try Self.parse(text: result.text)
    }

    // MARK: - Parsing helpers

    static func parse(text: String) throws -> ClaudeStatusSnapshot {
        guard !text.isEmpty else { throw ClaudeStatusProbeError.timedOut }

        let sessionPct = extractPercent(labelSubstring: "Current session", text: text)
        let weeklyPct = extractPercent(labelSubstring: "Current week (all models)", text: text)
        let opusPct = extractPercent(labelSubstring: "Current week (Opus)", text: text)
        let email = extractFirst(pattern: #"(?i)Account:\s+([^\s@]+@[^\s@]+)"#, text: text)
        let org = extractFirst(pattern: #"(?i)Org:\s*(.+)"#, text: text)

        if sessionPct == nil && weeklyPct == nil && opusPct == nil {
            throw ClaudeStatusProbeError.parseFailed(text.prefix(400).description)
        }

        return ClaudeStatusSnapshot(
            sessionPercentLeft: sessionPct,
            weeklyPercentLeft: weeklyPct,
            opusPercentLeft: opusPct,
            accountEmail: email,
            accountOrganization: org,
            rawText: text
        )
    }

    private static func extractPercent(labelSubstring: String, text: String) -> Int? {
        let lines = text.components(separatedBy: .newlines)
        for (idx, line) in lines.enumerated() {
            if line.lowercased().contains(labelSubstring.lowercased()) {
                let window = lines.dropFirst(idx).prefix(4)
                for candidate in window {
                    if let pct = percentFromLine(candidate) { return pct }
                }
            }
        }
        return nil
    }

    private static func percentFromLine(_ line: String) -> Int? {
        let pattern = #"([0-9]{1,3})%\s*(used|left)"#
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) else { return nil }
        let range = NSRange(line.startIndex..<line.endIndex, in: line)
        guard let match = regex.firstMatch(in: line, options: [], range: range),
              match.numberOfRanges >= 3,
              let valRange = Range(match.range(at: 1), in: line),
              let kindRange = Range(match.range(at: 2), in: line)
        else { return nil }
        let rawVal = Int(line[valRange]) ?? 0
        let isUsed = line[kindRange].lowercased().contains("used")
        return isUsed ? max(0, 100 - rawVal) : rawVal
    }

    private static func extractFirst(pattern: String, text: String) -> String? {
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) else { return nil }
        let range = NSRange(text.startIndex..<text.endIndex, in: text)
        guard let match = regex.firstMatch(in: text, options: [], range: range),
              match.numberOfRanges >= 2,
              let r = Range(match.range(at: 1), in: text) else { return nil }
        return String(text[r]).trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
