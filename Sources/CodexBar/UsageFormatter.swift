import Foundation

enum UsageFormatter {
    static func usageLine(remaining: Double, used: Double) -> String {
        String(format: "%.0f%% left (%.0f%% used)", remaining, used)
    }

    static func updatedString(from date: Date, now: Date = .init()) -> String {
        if let hours = Calendar.current.dateComponents([.hour], from: date, to: now).hour, hours < 24 {
            let rel = RelativeDateTimeFormatter()
            rel.unitsStyle = .abbreviated
            return "Updated \(rel.localizedString(for: date, relativeTo: now))"
        } else {
            return "Updated \(date.formatted(date: .omitted, time: .shortened))"
        }
    }
}
