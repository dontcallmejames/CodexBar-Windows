import Foundation

struct CodexStatusSnapshot {
    let credits: Double?
    let fiveHourPercentLeft: Int?
    let weeklyPercentLeft: Int?
    let fiveHourResetDescription: String?
    let weeklyResetDescription: String?
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
        let fiveLine = self.firstLine(matching: #"5h limit[^\n]*"#, text: clean)
        let weekLine = self.firstLine(matching: #"Weekly limit[^\n]*"#, text: clean)
        let fivePct = fiveLine.flatMap { TextParsing.firstInt(pattern: #"([0-9]{1,3})%\s+left"#, text: $0) }
        let weekPct = weekLine.flatMap { TextParsing.firstInt(pattern: #"([0-9]{1,3})%\s+left"#, text: $0) }
        let fiveReset = fiveLine.flatMap { Self.extractReset(from: $0) }
        let weekReset = weekLine.flatMap { Self.extractReset(from: $0) }
        if credits == nil, fivePct == nil, weekPct == nil {
            throw CodexStatusProbeError.parseFailed(clean.prefix(400).description)
        }
        return CodexStatusSnapshot(
            credits: credits,
            fiveHourPercentLeft: fivePct,
            weeklyPercentLeft: weekPct,
            fiveHourResetDescription: fiveReset,
            weeklyResetDescription: weekReset,
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

    private static func extractReset(from line: String) -> String? {
        guard let range = line.range(of: #"resets?\s+(.+)"#, options: [.regularExpression, .caseInsensitive]) else {
            return nil
        }
        let tail = line[range]
        // Drop the leading "resets" label.
        return tail
            .replacingOccurrences(of: "resets", with: "", options: .caseInsensitive)
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private static func firstLine(matching pattern: String, text: String) -> String? {
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) else { return nil }
        let nsrange = NSRange(text.startIndex..<text.endIndex, in: text)
        guard let match = regex.firstMatch(in: text, options: [], range: nsrange),
              let r = Range(match.range(at: 0), in: text)
        else { return nil }
        return String(text[r])
    }
}
