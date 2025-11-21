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
        case let .parseFailed(msg):
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
        guard TTYCommandRunner.which(self.codexBinary) != nil else { throw CodexStatusProbeError.codexNotInstalled }
        let script = "/status\n"
        let result = try runner.run(
            binary: self.codexBinary,
            send: script,
            options: .init(rows: 50, cols: 160, timeout: self.timeout))
        return try Self.parse(text: result.text)
    }

    // MARK: - Parsing

    static func parse(text: String) throws -> CodexStatusSnapshot {
        let clean = TextParsing.stripANSICodes(text)
        guard !clean.isEmpty else { throw CodexStatusProbeError.timedOut }
        if self.containsUpdatePrompt(clean) {
            throw CodexStatusProbeError.parseFailed(
                "Codex CLI shows an update prompt. Run `bun install -g @openai/codex` then retry.")
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

    private static func containsUpdatePrompt(_ text: String) -> Bool {
        let lower = text.lowercased()
        return lower.contains("update available") && lower.contains("codex")
    }
}
