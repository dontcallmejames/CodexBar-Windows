import AppKit
import Foundation

struct RateWindow: Codable {
    let usedPercent: Double
    let windowMinutes: Int?
    let resetsAt: Date?
    /// Optional textual reset description (used by Claude CLI UI scrape).
    let resetDescription: String?

    var remainingPercent: Double {
        max(0, 100 - self.usedPercent)
    }
}

struct UsageSnapshot {
    let primary: RateWindow
    let secondary: RateWindow
    let updatedAt: Date
    let accountEmail: String?
    let accountOrganization: String?

    init(
        primary: RateWindow,
        secondary: RateWindow,
        updatedAt: Date,
        accountEmail: String? = nil,
        accountOrganization: String? = nil
    ) {
        self.primary = primary
        self.secondary = secondary
        self.updatedAt = updatedAt
        self.accountEmail = accountEmail
        self.accountOrganization = accountOrganization
    }
}

struct AccountInfo: Equatable {
    let email: String?
    let plan: String?
}

enum UsageError: LocalizedError {
    case noSessions
    case noRateLimitsFound
    case decodeFailed

    var errorDescription: String? {
        switch self {
        case .noSessions:
            "No Codex sessions found yet. Run at least one Codex prompt first."
        case .noRateLimitsFound:
            "Found sessions, but no rate limit events yet."
        case .decodeFailed:
            "Could not parse Codex session log."
        }
    }
}

struct UsageFetcher: Sendable {
    private let codexHome: URL
    // Read only the last chunk of the newest session file first; fall back to full file if needed.
    private static let tailReadBytes: Int = 512 * 1024

    init(environment: [String: String] = ProcessInfo.processInfo.environment) {
        let home = environment["CODEX_HOME"] ?? "\(NSHomeDirectory())/.codex"
        self.codexHome = URL(fileURLWithPath: home)
    }

    func loadLatestUsage() async throws -> UsageSnapshot {
        let codexHome = self.codexHome
        // Do file IO off the main actor so menu updates stay snappy.
        return try await Task.detached(priority: .utility) {
            try Self.loadLatestUsageSync(fileManager: FileManager(), codexHome: codexHome)
        }.value
    }

    // MARK: - Sync helper for detached task

    private static func loadLatestUsageSync(fileManager: FileManager, codexHome: URL) throws -> UsageSnapshot {
        for sessionFile in try Self.sessionFilesSorted(fileManager: fileManager, codexHome: codexHome) {
            if let snapshot = try Self.loadUsage(from: sessionFile) {
                return snapshot
            }
        }

        throw UsageError.noRateLimitsFound
    }

    private static func loadUsage(from url: URL) throws -> UsageSnapshot? {
        // First try the tail of the newest file to avoid loading multi-MB logs.
        if let tail = try self.tailLines(url: url, bytes: self.tailReadBytes),
           let snapshot = self.parse(lines: tail) {
            return snapshot
        }

        // Fallback to full file scan for safety.
        let fullLines = try String(contentsOf: url, encoding: .utf8)
            .split(whereSeparator: \.isNewline)
            .map(String.init)
        return self.parse(lines: fullLines)
    }

    func loadAccountInfo() -> AccountInfo {
        let authURL = self.codexHome.appendingPathComponent("auth.json")
        guard let data = try? Data(contentsOf: authURL),
              let auth = try? JSONDecoder().decode(AuthFile.self, from: data),
              let idToken = auth.tokens?.idToken
        else {
            return AccountInfo(email: nil, plan: nil)
        }

        guard let payload = Self.parseJWT(idToken) else {
            return AccountInfo(email: nil, plan: nil)
        }

        let authDict = payload["https://api.openai.com/auth"] as? [String: Any]
        let profileDict = payload["https://api.openai.com/profile"] as? [String: Any]

        let plan = (authDict?["chatgpt_plan_type"] as? String)
            ?? (payload["chatgpt_plan_type"] as? String)

        let email = (payload["email"] as? String)
            ?? (profileDict?["email"] as? String)

        return AccountInfo(email: email, plan: plan)
    }

    private static func sessionFilesSorted(fileManager: FileManager, codexHome: URL) throws -> [URL] {
        let sessions = codexHome.appendingPathComponent("sessions")
        guard let enumerator = fileManager.enumerator(
            at: sessions,
            includingPropertiesForKeys: [.contentModificationDateKey])
        else {
            throw UsageError.noSessions
        }

        var files: [(url: URL, date: Date)] = []
        for case let url as URL in enumerator where url.lastPathComponent.hasPrefix("rollout-") {
            guard let date = try url.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate
            else { continue }
            files.append((url, date))
        }

        guard !files.isEmpty else { throw UsageError.noSessions }
        return files.sorted { $0.date > $1.date }.map(\.url)
    }

    private static func parseJWT(_ token: String) -> [String: Any]? {
        let parts = token.split(separator: ".")
        guard parts.count >= 2 else { return nil }
        let payloadPart = parts[1]

        var padded = String(payloadPart)
            .replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        while padded.count % 4 != 0 {
            padded.append("=")
        }
        guard let data = Data(base64Encoded: padded) else { return nil }
        guard let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return nil }
        return json
    }

    // MARK: - Decoding helpers

    private static func decodeFlexibleDate(_ any: Any?) -> Date? {
        guard let any else { return nil }
        if let d = any as? Double { return Date(timeIntervalSince1970: normalizeEpochSeconds(d)) }
        if let i = any as? Int { return Date(timeIntervalSince1970: normalizeEpochSeconds(Double(i))) }
        if let n = any as? NSNumber { return Date(timeIntervalSince1970: normalizeEpochSeconds(n.doubleValue)) }
        if let s = any as? String {
            // Support numeric epochs that may be seconds/millis/micros.
            if CharacterSet.decimalDigits.isSuperset(of: CharacterSet(charactersIn: s)), let val = Double(s) {
                return Date(timeIntervalSince1970: normalizeEpochSeconds(val))
            }
            // Fallback to ISO8601 with/without fractional seconds.
            let iso1 = ISO8601DateFormatter(); iso1.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
            if let d = iso1.date(from: s) { return d }
            let iso2 = ISO8601DateFormatter(); iso2.formatOptions = [.withInternetDateTime]
            if let d = iso2.date(from: s) { return d }
        }
        return nil
    }

    private static func normalizeEpochSeconds(_ value: Double) -> Double {
        if value > 1e14 { return value / 1_000_000 }
        if value > 1e11 { return value / 1_000 }
        return value
    }

    private static func decodeWindow(_ any: Any?, created: Date, capturedAt: Date?) -> RateWindow {
        guard let dict = any as? [String: Any] else {
            return RateWindow(usedPercent: 0, windowMinutes: nil, resetsAt: nil, resetDescription: nil)
        }

        let usedPercent: Double = {
            if let d = dict["used_percent"] as? Double { return d }
            if let i = dict["used_percent"] as? Int { return Double(i) }
            if let n = dict["used_percent"] as? NSNumber { return n.doubleValue }
            return 0
        }()

        let windowMinutes = (dict["window_minutes"] as? NSNumber)?.intValue

        var resetsAt: Date?
        // Accept absolute reset times in seconds or milliseconds under multiple server casings.
        let keys = ["resets_at", "reset_at", "resetsAt", "resetAt", "resets_at_ms", "reset_at_ms"]
        for key in keys {
            guard let raw = dict[key] else { continue }
            if key.hasSuffix("_ms") {
                if let num = raw as? NSNumber {
                    resetsAt = Date(timeIntervalSince1970: normalizeEpochSeconds(num.doubleValue))
                    break
                }
                if let num = raw as? Double {
                    resetsAt = Date(timeIntervalSince1970: normalizeEpochSeconds(num))
                    break
                }
                if let num = raw as? Int {
                    resetsAt = Date(timeIntervalSince1970: normalizeEpochSeconds(Double(num)))
                    break
                }
                if let s = raw as? String, let num = Double(s) {
                    resetsAt = Date(timeIntervalSince1970: normalizeEpochSeconds(num))
                    break
                }
            } else if let date = decodeFlexibleDate(raw) {
                resetsAt = date
                break
            }
        }

        // If captured_at is present and resetsAt was missing, assume capturedAt is the best freshness indicator.
        let finalReset = resetsAt ?? capturedAt ?? created

        return RateWindow(
            usedPercent: usedPercent,
            windowMinutes: windowMinutes,
            resetsAt: finalReset,
            resetDescription: nil)
    }

    private static func parse<S: Sequence>(lines: S) -> UsageSnapshot? where S.Element == String {
        for rawLine in lines.reversed() {
            guard let data = rawLine.data(using: .utf8) else { continue }
            guard let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { continue }

            // Prefer nested payload for consistency, but fall back to top-level.
            let payload = (json["payload"] as? [String: Any]) ?? json
            // Timestamps show up in multiple places depending on emitter; pick the first valid one.
            let createdAt = decodeFlexibleDate(json["timestamp"]) ??
                decodeFlexibleDate(payload["timestamp"]) ??
                decodeFlexibleDate(payload["created_at"]) ??
                Date()

            // Accept modern token_count and account/rateLimits update shapes.
            let type = (payload["type"] as? String)?.lowercased()
            guard type == "token_count" ||
                type?.contains("ratelimits") == true ||
                type?.contains("rate_limits") == true else {
                continue
            }

            // Modern logs: rate_limits attached to payload (or top-level fallback for safety).
            let rate = (payload["rate_limits"] as? [String: Any]) ??
                json["rate_limits"] as? [String: Any]
            guard let rate else { continue }

            let capturedAt = decodeFlexibleDate(rate["captured_at"]) ?? createdAt
            let primary = decodeWindow(rate["primary"], created: createdAt, capturedAt: capturedAt)
            let secondary = decodeWindow(rate["secondary"], created: createdAt, capturedAt: capturedAt)

            return UsageSnapshot(
                primary: primary,
                secondary: secondary,
                updatedAt: capturedAt)
        }
        return nil
    }

    private static func tailLines(url: URL, bytes: Int) throws -> [String]? {
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }

        let fileSize = (try handle.seekToEnd()) as UInt64
        guard fileSize > 0 else { return [] }

        let start = fileSize > bytes ? fileSize - UInt64(bytes) : 0
        try handle.seek(toOffset: start)
        let data = try handle.readToEnd() ?? Data()
        var string = String(decoding: data, as: UTF8.self)
        if start > 0 {
            // Drop the potentially partial first line when starting mid-file.
            if let newlineRange = string.range(of: "\n") {
                string = String(string[newlineRange.upperBound...])
            } else {
                // No newline at all; return nil to force fallback.
                return nil
            }
        }
        return string.split(whereSeparator: \.isNewline).map(String.init)
    }
}

private struct AuthFile: Decodable {
    let tokens: Tokens?
}

private struct Tokens: Decodable {
    let idToken: String?

    enum CodingKeys: String, CodingKey {
        case idToken = "id_token"
    }
}
