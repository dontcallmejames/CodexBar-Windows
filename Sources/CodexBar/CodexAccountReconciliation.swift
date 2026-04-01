import CodexBarCore
import Foundation

struct CodexVisibleAccount: Equatable, Sendable, Identifiable {
    let id: String
    let email: String
    let storedAccountID: UUID?
    let selectionSource: CodexActiveSource
    let isActive: Bool
    let isLive: Bool
    let canReauthenticate: Bool
    let canRemove: Bool
}

struct CodexVisibleAccountProjection: Equatable, Sendable {
    let visibleAccounts: [CodexVisibleAccount]
    let activeVisibleAccountID: String?
    let liveVisibleAccountID: String?
    let hasUnreadableAddedAccountStore: Bool

    func source(forVisibleAccountID id: String) -> CodexActiveSource? {
        self.visibleAccounts.first { $0.id == id }?.selectionSource
    }
}

struct CodexResolvedActiveSource: Equatable, Sendable {
    let persistedSource: CodexActiveSource
    let resolvedSource: CodexActiveSource

    var requiresPersistenceCorrection: Bool {
        self.persistedSource != self.resolvedSource
    }
}

enum CodexActiveSourceResolver {
    static func resolve(from snapshot: CodexAccountReconciliationSnapshot) -> CodexResolvedActiveSource {
        let persistedSource = snapshot.activeSource
        let resolvedSource: CodexActiveSource = switch persistedSource {
        case .liveSystem:
            .liveSystem
        case let .managedAccount(id):
            if let activeStoredAccount = snapshot.activeStoredAccount {
                self.matchesLiveSystemAccount(
                    storedAccount: activeStoredAccount,
                    snapshot: snapshot,
                    liveSystemAccount: snapshot.liveSystemAccount) ? .liveSystem : .managedAccount(id: id)
            } else {
                snapshot.liveSystemAccount != nil ? .liveSystem : .managedAccount(id: id)
            }
        }

        return CodexResolvedActiveSource(
            persistedSource: persistedSource,
            resolvedSource: resolvedSource)
    }

    private static func matchesLiveSystemAccount(
        storedAccount: ManagedCodexAccount,
        snapshot: CodexAccountReconciliationSnapshot,
        liveSystemAccount: ObservedSystemCodexAccount?) -> Bool
    {
        guard let liveSystemAccount else { return false }
        return CodexIdentityMatcher.matches(
            snapshot.runtimeIdentity(for: storedAccount),
            snapshot.runtimeIdentity(for: liveSystemAccount))
    }
}

struct CodexAccountReconciliationSnapshot: Equatable, Sendable {
    let storedAccounts: [ManagedCodexAccount]
    let activeStoredAccount: ManagedCodexAccount?
    let liveSystemAccount: ObservedSystemCodexAccount?
    let matchingStoredAccountForLiveSystemAccount: ManagedCodexAccount?
    let activeSource: CodexActiveSource
    let hasUnreadableAddedAccountStore: Bool
    let storedAccountRuntimeIdentities: [UUID: CodexIdentity]
    let storedAccountRuntimeEmails: [UUID: String]

    init(
        storedAccounts: [ManagedCodexAccount],
        activeStoredAccount: ManagedCodexAccount?,
        liveSystemAccount: ObservedSystemCodexAccount?,
        matchingStoredAccountForLiveSystemAccount: ManagedCodexAccount?,
        activeSource: CodexActiveSource,
        hasUnreadableAddedAccountStore: Bool,
        storedAccountRuntimeIdentities: [UUID: CodexIdentity] = [:],
        storedAccountRuntimeEmails: [UUID: String] = [:])
    {
        self.storedAccounts = storedAccounts
        self.activeStoredAccount = activeStoredAccount
        self.liveSystemAccount = liveSystemAccount
        self.matchingStoredAccountForLiveSystemAccount = matchingStoredAccountForLiveSystemAccount
        self.activeSource = activeSource
        self.hasUnreadableAddedAccountStore = hasUnreadableAddedAccountStore
        self.storedAccountRuntimeIdentities = storedAccountRuntimeIdentities
        self.storedAccountRuntimeEmails = storedAccountRuntimeEmails
    }

    static func == (lhs: CodexAccountReconciliationSnapshot, rhs: CodexAccountReconciliationSnapshot) -> Bool {
        lhs.storedAccounts.map(AccountIdentity.init) == rhs.storedAccounts.map(AccountIdentity.init)
            && lhs.activeStoredAccount.map(AccountIdentity.init) == rhs.activeStoredAccount.map(AccountIdentity.init)
            && lhs.liveSystemAccount == rhs.liveSystemAccount
            && lhs.matchingStoredAccountForLiveSystemAccount.map(AccountIdentity.init)
            == rhs.matchingStoredAccountForLiveSystemAccount.map(AccountIdentity.init)
            && lhs.activeSource == rhs.activeSource
            && lhs.hasUnreadableAddedAccountStore == rhs.hasUnreadableAddedAccountStore
            && lhs.storedAccountRuntimeIdentities == rhs.storedAccountRuntimeIdentities
            && lhs.storedAccountRuntimeEmails == rhs.storedAccountRuntimeEmails
    }

    func runtimeIdentity(for storedAccount: ManagedCodexAccount) -> CodexIdentity {
        self.storedAccountRuntimeIdentities[storedAccount.id]
            ?? CodexIdentityResolver.resolve(accountId: nil, email: storedAccount.email)
    }

    func runtimeEmail(for storedAccount: ManagedCodexAccount) -> String {
        self.storedAccountRuntimeEmails[storedAccount.id]
            ?? Self.normalizeEmail(storedAccount.email)
    }

    func runtimeIdentity(for liveSystemAccount: ObservedSystemCodexAccount) -> CodexIdentity {
        CodexIdentityMatcher.normalized(
            liveSystemAccount.identity,
            fallbackEmail: liveSystemAccount.email)
    }

    private static func normalizeEmail(_ email: String) -> String {
        email.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    }
}

struct DefaultCodexAccountReconciler {
    let storeLoader: @Sendable () throws -> ManagedCodexAccountSet
    let systemObserver: any CodexSystemAccountObserving
    let activeSource: CodexActiveSource

    init(
        storeLoader: @escaping @Sendable () throws -> ManagedCodexAccountSet = {
            try FileManagedCodexAccountStore().loadAccounts()
        },
        systemObserver: any CodexSystemAccountObserving = DefaultCodexSystemAccountObserver(),
        activeSource: CodexActiveSource = .liveSystem)
    {
        self.storeLoader = storeLoader
        self.systemObserver = systemObserver
        self.activeSource = activeSource
    }

    func loadSnapshot(environment: [String: String]) -> CodexAccountReconciliationSnapshot {
        let liveSystemAccount = self.loadLiveSystemAccount(environment: environment)

        do {
            let accounts = try self.storeLoader()
            let runtimeAccounts = Dictionary(uniqueKeysWithValues: accounts.accounts.map { account in
                let runtimeAccount = self.loadRuntimeAccount(for: account)
                return (account.id, runtimeAccount)
            })
            let activeStoredAccount: ManagedCodexAccount? = switch self.activeSource {
            case let .managedAccount(id):
                accounts.account(id: id)
            case .liveSystem:
                nil
            }
            let matchingStoredAccountForLiveSystemAccount = liveSystemAccount.flatMap { liveAccount in
                accounts.accounts.first { account in
                    guard let runtimeAccount = runtimeAccounts[account.id] else { return false }
                    return CodexIdentityMatcher.matches(runtimeAccount.identity, self.runtimeIdentity(for: liveAccount))
                }
            }

            return CodexAccountReconciliationSnapshot(
                storedAccounts: accounts.accounts,
                activeStoredAccount: activeStoredAccount,
                liveSystemAccount: liveSystemAccount,
                matchingStoredAccountForLiveSystemAccount: matchingStoredAccountForLiveSystemAccount,
                activeSource: self.activeSource,
                hasUnreadableAddedAccountStore: false,
                storedAccountRuntimeIdentities: runtimeAccounts.mapValues(\.identity),
                storedAccountRuntimeEmails: runtimeAccounts.mapValues(\.email))
        } catch {
            return CodexAccountReconciliationSnapshot(
                storedAccounts: [],
                activeStoredAccount: nil,
                liveSystemAccount: liveSystemAccount,
                matchingStoredAccountForLiveSystemAccount: nil,
                activeSource: self.activeSource,
                hasUnreadableAddedAccountStore: true)
        }
    }

    func loadVisibleAccounts(environment: [String: String]) -> CodexVisibleAccountProjection {
        CodexVisibleAccountProjection.make(from: self.loadSnapshot(environment: environment))
    }

    private func loadLiveSystemAccount(environment: [String: String]) -> ObservedSystemCodexAccount? {
        do {
            guard let account = try self.systemObserver.loadSystemAccount(environment: environment) else {
                return nil
            }
            let normalizedEmail = Self.normalizeEmail(account.email)
            guard !normalizedEmail.isEmpty else {
                return nil
            }
            return ObservedSystemCodexAccount(
                email: normalizedEmail,
                codexHomePath: account.codexHomePath,
                observedAt: account.observedAt,
                identity: self.runtimeIdentity(for: account))
        } catch {
            return nil
        }
    }

    private static func normalizeEmail(_ email: String) -> String {
        email.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    }

    private func loadRuntimeAccount(for account: ManagedCodexAccount) -> RuntimeManagedCodexAccount {
        let scopedEnvironment = ["CODEX_HOME": account.managedHomePath]
        let authBackedAccount = UsageFetcher(environment: scopedEnvironment).loadAuthBackedCodexAccount()
        let email = Self.normalizeEmail(authBackedAccount.email ?? account.email)
        let identity = CodexIdentityMatcher.normalized(authBackedAccount.identity, fallbackEmail: email)

        return RuntimeManagedCodexAccount(
            account: account,
            email: email,
            identity: identity)
    }

    private func runtimeIdentity(for liveSystemAccount: ObservedSystemCodexAccount) -> CodexIdentity {
        CodexIdentityMatcher.normalized(
            liveSystemAccount.identity,
            fallbackEmail: liveSystemAccount.email)
    }
}

extension CodexVisibleAccountProjection {
    static func make(from snapshot: CodexAccountReconciliationSnapshot) -> CodexVisibleAccountProjection {
        let resolvedActiveSource = CodexActiveSourceResolver.resolve(from: snapshot).resolvedSource
        var drafts: [VisibleAccountDraft] = []

        for storedAccount in snapshot.storedAccounts {
            let normalizedEmail = snapshot.runtimeEmail(for: storedAccount)
            drafts.append(VisibleAccountDraft(
                email: normalizedEmail,
                storedAccountID: storedAccount.id,
                selectionSource: .managedAccount(id: storedAccount.id),
                isLive: false,
                canReauthenticate: true,
                canRemove: true,
                identity: snapshot.runtimeIdentity(for: storedAccount)))
        }

        if let liveSystemAccount = snapshot.liveSystemAccount {
            let normalizedEmail = Self.normalizeVisibleEmail(liveSystemAccount.email)
            let liveIdentity = snapshot.runtimeIdentity(for: liveSystemAccount)
            if let existingIndex = drafts.firstIndex(where: { draft in
                CodexIdentityMatcher.matches(draft.identity, liveIdentity)
            }) {
                drafts[existingIndex] = VisibleAccountDraft(
                    email: drafts[existingIndex].email,
                    storedAccountID: drafts[existingIndex].storedAccountID,
                    selectionSource: .liveSystem,
                    isLive: true,
                    canReauthenticate: drafts[existingIndex].canReauthenticate,
                    canRemove: drafts[existingIndex].canRemove,
                    identity: liveIdentity)
            } else {
                drafts.append(VisibleAccountDraft(
                    email: normalizedEmail,
                    storedAccountID: nil,
                    selectionSource: .liveSystem,
                    isLive: true,
                    canReauthenticate: true,
                    canRemove: false,
                    identity: liveIdentity))
            }
        }

        let groupedByEmail = Dictionary(grouping: drafts.indices, by: { drafts[$0].email })
        let visibleAccounts = drafts.map { draft in
            let id = Self.visibleAccountID(for: draft, emailGroupSize: groupedByEmail[draft.email]?.count ?? 0)
            let isActive = switch resolvedActiveSource {
            case .liveSystem:
                draft.selectionSource == .liveSystem
            case let .managedAccount(id):
                draft.selectionSource == .managedAccount(id: id)
            }

            return CodexVisibleAccount(
                id: id,
                email: draft.email,
                storedAccountID: draft.storedAccountID,
                selectionSource: draft.selectionSource,
                isActive: isActive,
                isLive: draft.isLive,
                canReauthenticate: draft.canReauthenticate,
                canRemove: draft.canRemove)
        }.sorted { lhs, rhs in
            if lhs.email != rhs.email {
                return lhs.email < rhs.email
            }
            if lhs.isLive != rhs.isLive {
                return lhs.isLive && !rhs.isLive
            }
            return lhs.id < rhs.id
        }

        return CodexVisibleAccountProjection(
            visibleAccounts: visibleAccounts,
            activeVisibleAccountID: visibleAccounts.first { $0.isActive }?.id,
            liveVisibleAccountID: visibleAccounts.first { $0.isLive }?.id,
            hasUnreadableAddedAccountStore: snapshot.hasUnreadableAddedAccountStore)
    }

    private static func normalizeVisibleEmail(_ email: String) -> String {
        email.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    }

    private static func visibleAccountID(for draft: VisibleAccountDraft, emailGroupSize: Int) -> String {
        guard emailGroupSize > 1 else { return draft.email }

        switch draft.selectionSource {
        case .liveSystem:
            return "live:\(CodexIdentityMatcher.selectionKey(for: draft.identity, fallbackEmail: draft.email))"
        case let .managedAccount(id):
            return "managed:\(id.uuidString.lowercased())"
        }
    }
}

private enum CodexIdentityMatcher {
    static func matches(_ lhs: CodexIdentity, _ rhs: CodexIdentity) -> Bool {
        switch (lhs, rhs) {
        case let (.providerAccount(leftID), .providerAccount(rightID)):
            leftID == rightID
        case let (.emailOnly(leftEmail), .emailOnly(rightEmail)):
            leftEmail == rightEmail
        default:
            false
        }
    }

    static func normalized(_ identity: CodexIdentity, fallbackEmail: String) -> CodexIdentity {
        switch identity {
        case .providerAccount:
            identity
        case let .emailOnly(normalizedEmail):
            CodexIdentityResolver.resolve(accountId: nil, email: normalizedEmail)
        case .unresolved:
            CodexIdentityResolver.resolve(accountId: nil, email: fallbackEmail)
        }
    }

    static func selectionKey(for identity: CodexIdentity, fallbackEmail: String) -> String {
        switch self.normalized(identity, fallbackEmail: fallbackEmail) {
        case let .providerAccount(id):
            "provider:\(id)"
        case let .emailOnly(normalizedEmail):
            "email:\(normalizedEmail)"
        case .unresolved:
            "unresolved:\(fallbackEmail)"
        }
    }
}

private struct RuntimeManagedCodexAccount: Sendable {
    let account: ManagedCodexAccount
    let email: String
    let identity: CodexIdentity
}

private struct VisibleAccountDraft {
    let email: String
    let storedAccountID: UUID?
    let selectionSource: CodexActiveSource
    let isLive: Bool
    let canReauthenticate: Bool
    let canRemove: Bool
    let identity: CodexIdentity
}

private struct AccountIdentity: Equatable {
    let id: UUID
    let email: String
    let managedHomePath: String
    let createdAt: TimeInterval
    let updatedAt: TimeInterval
    let lastAuthenticatedAt: TimeInterval?

    init(_ account: ManagedCodexAccount) {
        self.id = account.id
        self.email = account.email
        self.managedHomePath = account.managedHomePath
        self.createdAt = account.createdAt
        self.updatedAt = account.updatedAt
        self.lastAuthenticatedAt = account.lastAuthenticatedAt
    }
}
