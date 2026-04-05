import CodexBarCore
import Foundation

enum CodexDisplacedLivePreservationNoneReason: Sendable, Equatable {
    case liveMissing
    case targetMatchesLiveAuthIdentity
}

enum CodexDisplacedLivePreservationRejectReason: Sendable, Equatable {
    case liveUnreadable
    case liveAPIKeyOnlyUnsupported
    case liveIdentityMissingForPreservation
}

enum CodexDisplacedLivePreservationImportReason: Sendable, Equatable {
    case noExistingManagedDestination
}

enum CodexDisplacedLivePreservationRefreshReason: Sendable, Equatable {
    case readableHomeIdentityMatch
    case readableHomeIdentityMatchUsingPersistedEmailFallback
}

enum CodexDisplacedLivePreservationRepairReason: Sendable, Equatable {
    case persistedProviderMatchWithMissingHome
    case persistedProviderMatchWithUnreadableHome
    case persistedProviderMatchWithConflictingReadableHome
    case persistedLegacyEmailMatch
}

enum CodexDisplacedLivePreservationPlan: Sendable {
    case none(reason: CodexDisplacedLivePreservationNoneReason)
    case reject(reason: CodexDisplacedLivePreservationRejectReason)
    case importNew(reason: CodexDisplacedLivePreservationImportReason)
    case refreshExisting(
        destination: PreparedStoredManagedAccount,
        reason: CodexDisplacedLivePreservationRefreshReason)
    case repairExisting(
        destination: PreparedStoredManagedAccount,
        reason: CodexDisplacedLivePreservationRepairReason)
}

struct CodexDisplacedLivePreservationPlanner {
    func makePlan(context: PreparedPromotionContext) -> CodexDisplacedLivePreservationPlan {
        switch context.live.homeState {
        case .missing:
            return .none(reason: .liveMissing)
        case .unreadable:
            return .reject(reason: .liveUnreadable)
        case .apiKeyOnly:
            return .reject(reason: .liveAPIKeyOnlyUnsupported)
        case .readable:
            break
        }

        guard let liveAuthIdentity = context.live.authIdentity else {
            return .reject(reason: .liveIdentityMissingForPreservation)
        }

        if let targetAuthIdentity = context.target.authIdentity,
           CodexIdentityMatcher.matches(targetAuthIdentity.identity, liveAuthIdentity.identity)
        {
            return .none(reason: .targetMatchesLiveAuthIdentity)
        }

        let candidates = context.storedManagedAccounts.filter { $0.persisted.id != context.target.persisted.id }
        if let destination = self.findReadableHomeMatch(in: candidates, liveAuthIdentity: liveAuthIdentity) {
            let reason: CodexDisplacedLivePreservationRefreshReason =
                if liveAuthIdentity.email == nil {
                    .readableHomeIdentityMatchUsingPersistedEmailFallback
                } else {
                    .readableHomeIdentityMatch
                }
            return .refreshExisting(destination: destination, reason: reason)
        }

        if let repaired = self.findPersistedRepairMatch(in: candidates, liveAuthIdentity: liveAuthIdentity) {
            return .repairExisting(destination: repaired.destination, reason: repaired.reason)
        }

        guard liveAuthIdentity.identity != .unresolved, liveAuthIdentity.email != nil else {
            return .reject(reason: .liveIdentityMissingForPreservation)
        }

        return .importNew(reason: .noExistingManagedDestination)
    }

    private func findReadableHomeMatch(
        in candidates: [PreparedStoredManagedAccount],
        liveAuthIdentity: PreparedIdentity)
        -> PreparedStoredManagedAccount?
    {
        candidates.first { candidate in
            guard let candidateAuthIdentity = candidate.authIdentity else { return false }
            return CodexIdentityMatcher.matches(candidateAuthIdentity.identity, liveAuthIdentity.identity)
        }
    }

    private func findPersistedRepairMatch(
        in candidates: [PreparedStoredManagedAccount],
        liveAuthIdentity: PreparedIdentity)
        -> (destination: PreparedStoredManagedAccount, reason: CodexDisplacedLivePreservationRepairReason)?
    {
        switch liveAuthIdentity.identity {
        case let .providerAccount(id):
            let providerAccountID = ManagedCodexAccount.normalizeProviderAccountID(id)
            if let destination = candidates.first(where: { $0.persisted.providerAccountID == providerAccountID }) {
                return (destination, self.providerRepairReason(for: destination, liveAuthIdentity: liveAuthIdentity))
            }

            if let liveEmail = liveAuthIdentity.email,
               let destination = candidates.first(where: {
                   $0.persisted.providerAccountID == nil && $0.persisted.email == liveEmail
               })
            {
                return (destination, .persistedLegacyEmailMatch)
            }

            return nil

        case let .emailOnly(normalizedEmail):
            guard let destination = candidates.first(where: {
                $0.persisted.providerAccountID == nil && $0.persisted.email == normalizedEmail
            }) else {
                return nil
            }
            return (destination, .persistedLegacyEmailMatch)

        case .unresolved:
            return nil
        }
    }

    private func providerRepairReason(
        for destination: PreparedStoredManagedAccount,
        liveAuthIdentity: PreparedIdentity)
        -> CodexDisplacedLivePreservationRepairReason
    {
        switch destination.homeState {
        case .missing:
            return .persistedProviderMatchWithMissingHome
        case .unreadable:
            return .persistedProviderMatchWithUnreadableHome
        case let .readable(authMaterial):
            if CodexIdentityMatcher.matches(authMaterial.authIdentity.identity, liveAuthIdentity.identity) {
                return .persistedProviderMatchWithConflictingReadableHome
            }
            return .persistedProviderMatchWithConflictingReadableHome
        }
    }
}
