import Foundation
import Testing
@testable import CodexBar

@Suite
struct CodexBarTests {
    @Test
    func iconRendererProducesTemplateImage() {
        let image = IconRenderer.makeIcon(primaryRemaining: 50, weeklyRemaining: 75, stale: false)
        #expect(image.isTemplate)
        #expect(image.size.width > 0)
    }

    @Test
    func usageFetcherParsesLatestTokenCount() async throws {
        let tmp = try FileManager.default.url(
            for: .itemReplacementDirectory,
            in: .userDomainMask,
            appropriateFor: URL(fileURLWithPath: NSTemporaryDirectory()),
            create: true)
        defer { try? FileManager.default.removeItem(at: tmp) }

        let sessions = tmp.appendingPathComponent("sessions/2025/11/16", isDirectory: true)
        try FileManager.default.createDirectory(at: sessions, withIntermediateDirectories: true)

        let event: [String: Any] = [
            "timestamp": "2025-11-16T18:00:00.000Z",
            "type": "event_msg",
            "payload": [
                "type": "token_count",
                "info": NSNull(),
                "rate_limits": [
                    "primary": [
                        "used_percent": 25.0,
                        "window_minutes": 300,
                        "resets_at": 1_763_320_800,
                    ],
                    "secondary": [
                        "used_percent": 60.0,
                        "window_minutes": 10080,
                        "resets_at": 1_763_608_000,
                    ],
                ],
            ],
        ]

        let data = try JSONSerialization.data(withJSONObject: event)
        let file = sessions.appendingPathComponent("rollout-2025-11-16T18-00-00.jsonl")
        try data.appendedNewline().write(to: file)

        // Make sure this file is the newest.
        try FileManager.default.setAttributes([.modificationDate: Date()], ofItemAtPath: file.path)

        let fetcher = UsageFetcher(environment: ["CODEX_HOME": tmp.path])
        let snapshot = try await fetcher.loadLatestUsage()

        #expect(snapshot.primary.usedPercent.isApproximatelyEqual(to: 25.0, absoluteTolerance: 0.01))
        #expect(snapshot.secondary.usedPercent.isApproximatelyEqual(to: 60.0, absoluteTolerance: 0.01))
        #expect(snapshot.primary.windowMinutes == 300)
        #expect(snapshot.secondary.windowMinutes == 10080)

        let formatter = ISO8601DateFormatter()
        formatter.formatOptions.insert(.withFractionalSeconds)
        let expectedDate = formatter.date(from: "2025-11-16T18:00:00.000Z")
        #expect(snapshot.updatedAt == expectedDate)
    }

    @Test
    func usageFetcherErrorsWhenNoTokenCount() async throws {
        let tmp = try FileManager.default.url(
            for: .itemReplacementDirectory,
            in: .userDomainMask,
            appropriateFor: URL(fileURLWithPath: NSTemporaryDirectory()),
            create: true)
        defer { try? FileManager.default.removeItem(at: tmp) }

        let sessions = tmp.appendingPathComponent("sessions/2025/11/16", isDirectory: true)
        try FileManager.default.createDirectory(at: sessions, withIntermediateDirectories: true)

        let file = sessions.appendingPathComponent("rollout-2025-11-16T10-00-00.jsonl")
        try "{\"timestamp\":\"2025-11-16T10:00:00.000Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"other\"}}\n"
            .write(to: file, atomically: true, encoding: .utf8)

        let fetcher = UsageFetcher(environment: ["CODEX_HOME": tmp.path])
        await #expect(throws: UsageError.noRateLimitsFound) {
            _ = try await fetcher.loadLatestUsage()
        }
    }

    @Test
    func usageFetcherParsesRateLimitsWithMilliseconds() async throws {
        let tmp = try FileManager.default.url(
            for: .itemReplacementDirectory,
            in: .userDomainMask,
            appropriateFor: URL(fileURLWithPath: NSTemporaryDirectory()),
            create: true)
        defer { try? FileManager.default.removeItem(at: tmp) }

        let sessions = tmp.appendingPathComponent("sessions/2025/11/17", isDirectory: true)
        try FileManager.default.createDirectory(at: sessions, withIntermediateDirectories: true)

        let event: [String: Any] = [
            "timestamp": "2025-11-17T12:00:00.000Z",
            "type": "event_msg",
            "payload": [
                "type": "token_count",
                "rate_limits": [
                    "primary": [
                        "used_percent": 55,
                        "window_minutes": 300,
                        "resets_at": 1_700_000_000,
                    ],
                    "secondary": [
                        "used_percent": 12,
                        "window_minutes": 10080,
                        "reset_at_ms": 1_800_000_000_000,
                    ],
                ],
            ],
        ]

        let data = try JSONSerialization.data(withJSONObject: event)
        let file = sessions.appendingPathComponent("rollout-2025-11-17T12-00-00.jsonl")
        try data.appendedNewline().write(to: file)
        try FileManager.default.setAttributes([.modificationDate: Date()], ofItemAtPath: file.path)

        let fetcher = UsageFetcher(environment: ["CODEX_HOME": tmp.path])
        let snapshot = try await fetcher.loadLatestUsage()

        #expect(snapshot.primary.usedPercent.isApproximatelyEqual(to: 55, absoluteTolerance: 0.001))
        #expect(snapshot.primary.windowMinutes == 300)
        #expect(snapshot.secondary.usedPercent.isApproximatelyEqual(to: 12, absoluteTolerance: 0.001))
        #expect(snapshot.secondary.windowMinutes == 10080)

        #expect(snapshot.primary.resetsAt != nil)
        #expect(snapshot.secondary.resetsAt != nil)
    }

    @Test
    func usageFetcherParsesAccountRateLimitUpdatedEvents() async throws {
        let tmp = try FileManager.default.url(
            for: .itemReplacementDirectory,
            in: .userDomainMask,
            appropriateFor: URL(fileURLWithPath: NSTemporaryDirectory()),
            create: true)
        defer { try? FileManager.default.removeItem(at: tmp) }

        let sessions = tmp.appendingPathComponent("sessions/2025/11/17", isDirectory: true)
        try FileManager.default.createDirectory(at: sessions, withIntermediateDirectories: true)

        let event: [String: Any] = [
            "timestamp": 1_800_000_000.0,
            "type": "event_msg",
            "payload": [
                "type": "account/rateLimits/updated",
                "rate_limits": [
                    "primary": [
                        "used_percent": 5,
                        "window_minutes": 300,
                        "resets_at": 1_800_000_000.0,
                    ],
                    "secondary": [
                        "used_percent": 8,
                        "window_minutes": 10080,
                        "resets_at": 1_800_050_000.0,
                    ],
                ],
            ],
        ]

        let data = try JSONSerialization.data(withJSONObject: event)
        let file = sessions.appendingPathComponent("rollout-2025-11-17T12-30-00.jsonl")
        try data.appendedNewline().write(to: file)
        try FileManager.default.setAttributes([.modificationDate: Date()], ofItemAtPath: file.path)

        let fetcher = UsageFetcher(environment: ["CODEX_HOME": tmp.path])
        let snapshot = try await fetcher.loadLatestUsage()

        #expect(snapshot.primary.usedPercent.isApproximatelyEqual(to: 5, absoluteTolerance: 0.001))
        #expect(snapshot.secondary.usedPercent.isApproximatelyEqual(to: 8, absoluteTolerance: 0.001))
        #expect(snapshot.updatedAt.timeIntervalSince1970 == 1_800_000_000.0)
    }

    @Test
    func usageFetcherTailsLargeNewestFile() async throws {
        let tmp = try FileManager.default.url(
            for: .itemReplacementDirectory,
            in: .userDomainMask,
            appropriateFor: URL(fileURLWithPath: NSTemporaryDirectory()),
            create: true)
        defer { try? FileManager.default.removeItem(at: tmp) }

        let sessions = tmp.appendingPathComponent("sessions/2025/11/17", isDirectory: true)
        try FileManager.default.createDirectory(at: sessions, withIntermediateDirectories: true)

        let file = sessions.appendingPathComponent("rollout-2025-11-17T01-00-00.jsonl")

        // Build a ~700 KB file whose rate_limits event is near EOF so tail-read hits it.
        var lines: [String] = []
        let filler = String(repeating: "x", count: 900) // ~0.9 KB per line
        for _ in 0..<700 { lines.append("\"\(filler)\"") } // non-JSON we will fail to parse

        let event: [String: Any] = [
            "timestamp": "2025-11-17T01:59:59.000Z",
            "type": "event_msg",
            "payload": [
                "type": "token_count",
                "rate_limits": [
                    "primary": ["used_percent": 33],
                    "secondary": ["used_percent": 44],
                ],
            ],
        ]
        let data = try JSONSerialization.data(withJSONObject: event)
        lines.append(String(decoding: data, as: UTF8.self))
        try lines.joined(separator: "\n").write(to: file, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.modificationDate: Date()], ofItemAtPath: file.path)

        let fetcher = UsageFetcher(environment: ["CODEX_HOME": tmp.path])
        let snapshot = try await fetcher.loadLatestUsage()
        #expect(snapshot.primary.usedPercent.isApproximatelyEqual(to: 33, absoluteTolerance: 0.01))
        #expect(snapshot.secondary.usedPercent.isApproximatelyEqual(to: 44, absoluteTolerance: 0.01))
    }

    @Test
    func usageFetcherFallsBackWhenTailMisses() async throws {
        let tmp = try FileManager.default.url(
            for: .itemReplacementDirectory,
            in: .userDomainMask,
            appropriateFor: URL(fileURLWithPath: NSTemporaryDirectory()),
            create: true)
        defer { try? FileManager.default.removeItem(at: tmp) }

        let sessions = tmp.appendingPathComponent("sessions/2025/11/18", isDirectory: true)
        try FileManager.default.createDirectory(at: sessions, withIntermediateDirectories: true)

        let file = sessions.appendingPathComponent("rollout-2025-11-18T08-00-00.jsonl")

        // Place the rate_limits event at the start of a large file so tail-read won't see it.
        let event: [String: Any] = [
            "timestamp": "2025-11-18T08:00:00Z",
            "payload": [
                "type": "token_count",
                "rate_limits": [
                    "primary": ["used_percent": 11],
                    "secondary": ["used_percent": 22],
                ],
            ],
        ]
        var lines: [String] = [String(decoding: try JSONSerialization.data(withJSONObject: event), as: UTF8.self)]
        let filler = String(repeating: "y", count: 1200)
        for _ in 0..<800 { lines.append("\"\(filler)\"") } // pushes size past tail window
        try lines.joined(separator: "\n").write(to: file, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.modificationDate: Date()], ofItemAtPath: file.path)

        let fetcher = UsageFetcher(environment: ["CODEX_HOME": tmp.path])
        let snapshot = try await fetcher.loadLatestUsage()
        #expect(snapshot.primary.usedPercent.isApproximatelyEqual(to: 11, absoluteTolerance: 0.01))
        #expect(snapshot.secondary.usedPercent.isApproximatelyEqual(to: 22, absoluteTolerance: 0.01))
    }
}

extension Data {
    fileprivate func appendedNewline() -> Data {
        var result = self
        result.append(0x0A)
        return result
    }
}

extension Double {
    fileprivate func isApproximatelyEqual(to other: Double, absoluteTolerance: Double) -> Bool {
        abs(self - other) <= absoluteTolerance
    }
}
