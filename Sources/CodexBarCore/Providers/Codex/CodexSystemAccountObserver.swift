import Foundation

public struct ObservedSystemCodexAccount: Equatable, Sendable {
    public let email: String
    public let codexHomePath: String
    public let observedAt: Date
    public let identity: CodexIdentity

    public init(
        email: String,
        codexHomePath: String,
        observedAt: Date,
        identity: CodexIdentity = .unresolved)
    {
        self.email = email
        self.codexHomePath = codexHomePath
        self.observedAt = observedAt
        self.identity = identity
    }
}

public protocol CodexSystemAccountObserving: Sendable {
    func loadSystemAccount(environment: [String: String]) throws -> ObservedSystemCodexAccount?
}

public struct DefaultCodexSystemAccountObserver: CodexSystemAccountObserving {
    public init() {}

    public func loadSystemAccount(environment: [String: String]) throws -> ObservedSystemCodexAccount? {
        let homeURL = CodexHomeScope.ambientHomeURL(env: environment)
        let fetcher = UsageFetcher(environment: environment)
        let account = fetcher.loadAuthBackedCodexAccount()

        guard let rawEmail = account.email?.trimmingCharacters(in: .whitespacesAndNewlines),
              !rawEmail.isEmpty
        else {
            return nil
        }

        return ObservedSystemCodexAccount(
            email: rawEmail.lowercased(),
            codexHomePath: homeURL.path,
            observedAt: Date(),
            identity: account.identity)
    }
}
