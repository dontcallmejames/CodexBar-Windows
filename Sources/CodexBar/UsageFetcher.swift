import AppKit
import Foundation

struct RateWindow: Codable {
    let usedPercent: Double
    let windowMinutes: Int?
    let resetsAt: Date?

    var remainingPercent: Double {
        max(0, 100 - self.usedPercent)
    }
}

struct UsageSnapshot {
    let primary: RateWindow
    let secondary: RateWindow
    let updatedAt: Date
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
            return "No Codex sessions found yet. Run at least one Codex prompt first."
        case .noRateLimitsFound:
            return "Found sessions, but no rate limit events yet."
        case .decodeFailed:
            return "Could not parse Codex session log."
        }
    }
}

final class UsageFetcher {
    private let fileManager: FileManager
    private let codexHome: URL

    init(fileManager: FileManager = .default, environment: [String: String] = ProcessInfo.processInfo.environment) {
        self.fileManager = fileManager
        let home = environment["CODEX_HOME"] ?? "\(NSHomeDirectory())/.codex"
        self.codexHome = URL(fileURLWithPath: home)
    }

    func loadLatestUsage() throws -> UsageSnapshot {
        let sessionFile = try self.latestSessionFile()
        let lines = try String(contentsOf: sessionFile, encoding: .utf8).split(whereSeparator: \.isNewline)

        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601

        for lineSub in lines.reversed() {
            guard let data = lineSub.data(using: .utf8) else { continue }
            guard let event = try? decoder.decode(SessionLine.self, from: data) else { continue }
            guard event.payload?.type == "token_count", let limits = event.payload?.rateLimits else { continue }

            return UsageSnapshot(
                primary: limits.primary.rateWindow,
                secondary: limits.secondary.rateWindow,
                updatedAt: event.timestamp ?? Date())
        }

        throw UsageError.noRateLimitsFound
    }

    func loadAccountInfo() -> AccountInfo {
        let authURL = self.codexHome.appendingPathComponent("auth.json")
        guard let data = try? Data(contentsOf: authURL),
              let auth = try? JSONDecoder().decode(AuthFile.self, from: data),
              let idToken = auth.tokens?.idToken else {
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

    private func latestSessionFile() throws -> URL {
        let sessions = self.codexHome.appendingPathComponent("sessions")
        guard let enumerator = self.fileManager.enumerator(at: sessions, includingPropertiesForKeys: [.contentModificationDateKey]) else {
            throw UsageError.noSessions
        }

        var newest: (url: URL, date: Date)?
        for case let url as URL in enumerator where url.lastPathComponent.hasPrefix("rollout-") {
            guard let date = try url.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate else { continue }
            if newest == nil || date > newest!.date { newest = (url, date) }
        }

        guard let found = newest else { throw UsageError.noSessions }
        return found.url
    }

    private static func parseJWT(_ token: String) -> [String: Any]? {
        let parts = token.split(separator: ".")
        guard parts.count >= 2 else { return nil }
        let payloadPart = parts[1]

        var padded = String(payloadPart)
            .replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        while padded.count % 4 != 0 { padded.append("=") }
        guard let data = Data(base64Encoded: padded) else { return nil }
        guard let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return nil }
        return json
    }
}

// MARK: - Decoding helpers

private struct SessionLine: Decodable {
    let timestamp: Date?
    let payload: Payload?

    struct Payload: Decodable {
        let type: String?
        let rateLimits: RateLimits?

        enum CodingKeys: String, CodingKey {
            case type
            case rateLimits = "rate_limits"
        }
    }
}

private struct RateLimits: Decodable {
    let primary: Window
    let secondary: Window
}

private struct Window: Decodable {
    let usedPercent: Double
    let windowMinutes: Int?
    let resetsAt: TimeInterval?

    enum CodingKeys: String, CodingKey {
        case usedPercent = "used_percent"
        case windowMinutes = "window_minutes"
        case resetsAt = "resets_at"
    }

    var rateWindow: RateWindow {
        RateWindow(
            usedPercent: self.usedPercent,
            windowMinutes: self.windowMinutes,
            resetsAt: self.resetsAt.flatMap { Date(timeIntervalSince1970: $0) })
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
