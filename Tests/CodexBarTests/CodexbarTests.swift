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
