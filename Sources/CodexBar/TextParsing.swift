import Foundation

enum TextParsing {
    /// Removes ANSI escape sequences so regex parsing works on colored terminal output.
    static func stripANSICodes(_ text: String) -> String {
        // CSI sequences: ESC [ ... ending in 0x40–0x7E
        let pattern = #"\u001B\[[0-?]*[ -/]*[@-~]"#
        guard let regex = try? NSRegularExpression(pattern: pattern, options: []) else { return text }
        let range = NSRange(text.startIndex..<text.endIndex, in: text)
        return regex.stringByReplacingMatches(in: text, options: [], range: range, withTemplate: "")
    }

    static func firstNumber(pattern: String, text: String) -> Double? {
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) else { return nil }
        let range = NSRange(text.startIndex..<text.endIndex, in: text)
        guard let match = regex.firstMatch(in: text, options: [], range: range),
              match.numberOfRanges >= 2,
              let r = Range(match.range(at: 1), in: text) else { return nil }
        let raw = text[r].replacingOccurrences(of: ",", with: "")
        return Double(raw)
    }

    static func firstInt(pattern: String, text: String) -> Int? {
        guard let v = firstNumber(pattern: pattern, text: text) else { return nil }
        return Int(v)
    }
}
