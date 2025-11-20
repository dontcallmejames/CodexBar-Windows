import Foundation

struct CreditEvent: Identifiable, Equatable {
    let id = UUID()
    let date: Date
    let service: String
    let creditsUsed: Double
}

struct CreditsSnapshot: Equatable {
    let remaining: Double
    let events: [CreditEvent]
    let updatedAt: Date
}
