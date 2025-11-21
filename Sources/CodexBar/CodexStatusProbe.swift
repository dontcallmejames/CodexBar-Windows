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
    case updateRequired(String)

    var errorDescription: String? {
        switch self {
        case .codexNotInstalled:
            "Codex CLI is not installed or not on PATH."
        case .parseFailed:
            "Could not parse Codex status; will retry shortly."
        case .timedOut:
            "Codex status probe timed out."
        case let .updateRequired(msg):
            "Codex CLI update needed: \(msg)"
        }
    }
}

/// Runs `codex` inside a PTY, sends `/status`, captures text, and parses credits/limits.
struct CodexStatusProbe {
    var codexBinary: String = "codex"
    var timeout: TimeInterval = 18.0

    func fetch() async throws -> CodexStatusSnapshot {
        guard TTYCommandRunner.which(self.codexBinary) != nil else { throw CodexStatusProbeError.codexNotInstalled }
        do {
            return try self.runAndParse(rows: 60, cols: 200, timeout: self.timeout)
        } catch let error as CodexStatusProbeError {
            // Codex sometimes returns an incomplete screen on the first try; retry once with a longer window.
            switch error {
            case .parseFailed, .timedOut:
                return try self.runAndParse(rows: 70, cols: 220, timeout: max(self.timeout, 24.0))
            default:
                throw error
            }
        }
    }

    // MARK: - Parsing

    static func parse(text: String) throws -> CodexStatusSnapshot {
        let clean = TextParsing.stripANSICodes(text)
        guard !clean.isEmpty else { throw CodexStatusProbeError.timedOut }
        if clean.localizedCaseInsensitiveContains("data not available yet") {
            throw CodexStatusProbeError.parseFailed("data not available yet")
        }
        if self.containsUpdatePrompt(clean) {
            throw CodexStatusProbeError.updateRequired(
                "Run `bun install -g @openai/codex` to continue (update prompt blocking /status).")
        }
        let credits = TextParsing.firstNumber(pattern: #"Credits:\s*([0-9][0-9.,]*)"#, text: clean)
        let fivePct = TextParsing.firstInt(pattern: #"5h limit[^\\n]*?([0-9]{1,3})%\s+left"#, text: clean)
        let weekPct = TextParsing.firstInt(pattern: #"Weekly limit[^\\n]*?([0-9]{1,3})%\s+left"#, text: clean)
        if credits == nil, fivePct == nil, weekPct == nil {
            throw CodexStatusProbeError.parseFailed(clean.prefix(400).description)
        }
        return CodexStatusSnapshot(
            credits: credits,
            fiveHourPercentLeft: fivePct,
            weeklyPercentLeft: weekPct,
            rawText: clean)
    }

    private func runAndParse(rows: UInt16, cols: UInt16, timeout: TimeInterval) throws -> CodexStatusSnapshot {
        let runner = TTYCommandRunner()
        let script = "/status\n"
        let result = try runner.run(
            binary: self.codexBinary,
            send: script,
            options: .init(
                rows: rows,
                cols: cols,
                timeout: timeout,
                extraArgs: ["-s", "read-only", "-a", "untrusted"]))
        return try Self.parse(text: result.text)
    }

    private static func containsUpdatePrompt(_ text: String) -> Bool {
        let lower = text.lowercased()
        return lower.contains("update available") && lower.contains("codex")
    }
}
