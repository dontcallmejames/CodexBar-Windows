import Foundation
import Foundation

struct CodexStatusSnapshot {
    let credits: Double?
    let fiveHourPercentLeft: Int?
    let weeklyPercentLeft: Int?
    let rawText: String
}

enum CodexStatusProbeError: LocalizedError {
    case codexNotInstalled
    case parseFailed(String)
    case timedOut

    var errorDescription: String? {
        switch self {
        case .codexNotInstalled:
            "Codex CLI is not installed or not on PATH."
        case .parseFailed(let msg):
            "Could not parse codex status: \(msg)"
        case .timedOut:
            "Codex status probe timed out."
        }
    }
}

/// Runs `codex` inside a PTY, sends `/status`, captures text, and parses credits/limits.
struct CodexStatusProbe {
    var codexBinary: String = "codex"
    var timeout: TimeInterval = 8.0

    func fetch() async throws -> CodexStatusSnapshot {
        let runner = TTYCommandRunner()
        guard TTYCommandRunner.which(codexBinary) != nil else { throw CodexStatusProbeError.codexNotInstalled }
        let script = "/status\n"
        let result = try runner.run(binary: codexBinary, send: script, options: .init(rows: 50, cols: 160, timeout: timeout))
        return try Self.parse(text: result.text)
    }

    // MARK: - Parsing

    static func parse(text: String) throws -> CodexStatusSnapshot {
        guard !text.isEmpty else { throw CodexStatusProbeError.timedOut }
        let credits = firstNumber(pattern: #"Credits:\s*([0-9][0-9.,]*)"#, text: text)
        let fivePct = firstInt(pattern: #"5h limit[^\\n]*?([0-9]{1,3})%\s+left"#, text: text)
        let weekPct = firstInt(pattern: #"Weekly limit[^\\n]*?([0-9]{1,3})%\s+left"#, text: text)
        if credits == nil && fivePct == nil && weekPct == nil {
            throw CodexStatusProbeError.parseFailed(text.prefix(400).description)
        }
        return CodexStatusSnapshot(credits: credits,
                                   fiveHourPercentLeft: fivePct,
                                   weeklyPercentLeft: weekPct,
                                   rawText: text)
    }

    private static func firstNumber(pattern: String, text: String) -> Double? {
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) else { return nil }
        let range = NSRange(text.startIndex..<text.endIndex, in: text)
        guard let match = regex.firstMatch(in: text, options: [], range: range),
              match.numberOfRanges >= 2,
              let r = Range(match.range(at: 1), in: text) else { return nil }
        let raw = text[r].replacingOccurrences(of: ",", with: "")
        return Double(raw)
    }

    private static func firstInt(pattern: String, text: String) -> Int? {
        guard let v = firstNumber(pattern: pattern, text: text) else { return nil }
        return Int(v)
    }
}
