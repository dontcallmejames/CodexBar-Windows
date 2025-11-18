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

    static func creditsString(from value: Double) -> String {
        let number = NumberFormatter()
        number.numberStyle = .decimal
        number.maximumFractionDigits = 2
        let formatted = number.string(from: NSNumber(value: value)) ?? String(Int(value))
        return "\(formatted) left"
    }

    static func creditEventSummary(_ event: CreditEvent) -> String {
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        let number = NumberFormatter()
        number.numberStyle = .decimal
        number.maximumFractionDigits = 2
        let credits = number.string(from: NSNumber(value: event.creditsUsed)) ?? "0"
        return "\(formatter.string(from: event.date)) · \(event.service) · \(credits) credits"
    }

    static func creditEventCompact(_ event: CreditEvent) -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "MMM d"
        let number = NumberFormatter()
        number.numberStyle = .decimal
        number.maximumFractionDigits = 2
        let credits = number.string(from: NSNumber(value: event.creditsUsed)) ?? "0"
        return "\(formatter.string(from: event.date)) — \(event.service): \(credits)"
    }
}
