import Foundation

@MainActor
struct MenuDescriptor {
    struct Section {
        var entries: [Entry]
    }

    enum Entry {
        case text(String, TextStyle)
        case action(String, MenuAction)
        case divider
    }

    enum TextStyle {
        case headline
        case primary
        case secondary
    }

    enum MenuAction {
        case refresh
        case dashboard
        case settings
        case about
        case quit
        case copyError(String)
    }

    var sections: [Section]

    static func build(
        provider: UsageProvider?,
        store: UsageStore,
        settings: SettingsStore,
        account: AccountInfo) -> MenuDescriptor
    {
        var sections: [Section] = []

        func versionNumber(for provider: UsageProvider) -> String? {
            guard let raw = store.version(for: provider) else { return nil }
            let pattern = #"[0-9]+(?:\.[0-9]+)*"#
            guard let regex = try? NSRegularExpression(pattern: pattern) else { return nil }
            let range = NSRange(raw.startIndex..<raw.endIndex, in: raw)
            guard let match = regex.firstMatch(in: raw, options: [], range: range),
                  let r = Range(match.range, in: raw) else { return nil }
            return String(raw[r])
        }

        func usageSection(for provider: UsageProvider, titlePrefix: String) -> Section {
            let meta = store.metadata(for: provider)
            var entries: [Entry] = []
            let headlineText: String = {
                if let ver = versionNumber(for: provider) { return "\(meta.displayName) \(ver)" }
                return meta.displayName
            }()
            let headline = Entry.text(headlineText, .headline)

            func resetLine(_ reset: String) -> String {
                let trimmed = reset.trimmingCharacters(in: .whitespacesAndNewlines)
                if trimmed.lowercased().hasPrefix("resets") { return trimmed }
                return "Resets \(trimmed)"
            }

            entries.append(headline)
            if let snap = store.snapshot(for: provider) {
                let sessionLine = UsageFormatter
                    .usageLine(remaining: snap.primary.remainingPercent, used: snap.primary.usedPercent)
                entries.append(.text("\(meta.sessionLabel): \(sessionLine)", .primary))
                if let reset = snap.primary.resetDescription { entries.append(.text(resetLine(reset), .secondary)) }

                let weeklyLine = UsageFormatter
                    .usageLine(remaining: snap.secondary.remainingPercent, used: snap.secondary.usedPercent)
                entries.append(.text("\(meta.weeklyLabel): \(weeklyLine)", .primary))
                if let reset = snap.secondary.resetDescription { entries.append(.text(resetLine(reset), .secondary)) }

                if meta.supportsOpus, let opus = snap.tertiary {
                    let opusTitle = meta.opusLabel ?? "Opus"
                    let opusLine = UsageFormatter.usageLine(remaining: opus.remainingPercent, used: opus.usedPercent)
                    entries.append(.text("\(opusTitle): \(opusLine)", .primary))
                    if let reset = opus.resetDescription { entries.append(.text(resetLine(reset), .secondary)) }
                }
            } else {
                entries.append(.text("No usage yet", .secondary))
                if let err = store.error(for: provider), !err.isEmpty {
                    let title = UsageFormatter.truncatedSingleLine(err, max: 80)
                    entries.append(.action(title, .copyError(err)))
                }
            }

            if meta.supportsCredits, provider == .codex {
                if let credits = store.credits {
                    entries.append(.text("Credits: \(UsageFormatter.creditsString(from: credits.remaining))", .primary))
                    if let latest = credits.events.first {
                        entries.append(.text("Last spend: \(UsageFormatter.creditEventSummary(latest))", .secondary))
                    }
                } else {
                    let hint = store.lastCreditsError ?? meta.creditsHint
                    entries.append(.text(hint, .secondary))
                }
            }
            return Section(entries: entries)
        }

        /// Builds the account section.
        /// - Claude snapshot is preferred when `preferClaude` is true.
        /// - Otherwise Codex snapshot wins; falls back to stored auth info.
        func accountSection(
            preferred claude: UsageSnapshot?,
            codex: UsageSnapshot?,
            preferClaude: Bool) -> Section
        {
            var entries: [Entry] = []
            let emailFromClaude = claude?.accountEmail
            let emailFromCodex = codex?.accountEmail
            let planFromClaude = claude?.loginMethod
            let planFromCodex = codex?.loginMethod

            // Email: Claude wins when requested; otherwise Codex snapshot then auth.json fallback.
            let emailText: String = {
                if preferClaude, let e = emailFromClaude, !e.isEmpty { return Self.capFirst(e) }
                if let e = emailFromCodex, !e.isEmpty { return Self.capFirst(e) }
                if let codexEmail = account.email, !codexEmail.isEmpty { return Self.capFirst(codexEmail) }
                if let e = emailFromClaude, !e.isEmpty { return Self.capFirst(e) }
                return "Unknown"
            }()
            entries.append(.text("Account: \(emailText)", .secondary))

            // Plan: show only Claude plan when in Claude mode; otherwise Codex plan.
            if preferClaude {
                if let plan = planFromClaude, !plan.isEmpty {
                    entries.append(.text("Plan: \(plan)", .secondary))
                }
            } else if let plan = planFromCodex, !plan.isEmpty {
                entries.append(.text("Plan: \(plan)", .secondary))
            } else if let plan = account.plan, !plan.isEmpty {
                entries.append(.text("Plan: \(plan)", .secondary))
            }

            return Section(entries: entries)
        }

        func refreshStatusText(for provider: UsageProvider?) -> String {
            // Single place that decides what to show under the refresh button.
            if store.isRefreshing { return "Refreshing..." }
            let target = provider ?? store.enabledProviders().first
            if let target,
               let err = store.error(for: target),
               !err.isEmpty
            {
                return UsageFormatter.truncatedSingleLine(err, max: 80)
            }
            if let target,
               let updated = store.snapshot(for: target)?.updatedAt
            {
                return UsageFormatter.updatedString(from: updated)
            }
            return "Not fetched yet"
        }

        func actionsSection(for provider: UsageProvider?) -> Section {
            Section(entries: [
                .action("Refresh now", .refresh),
                .text(refreshStatusText(for: provider), .secondary),
                .action("Usage Dashboard", .dashboard),
            ])
        }

        func metaSection() -> Section {
            Section(entries: [
                .action("Settings...", .settings),
                .action("About CodexBar", .about),
                .action("Quit", .quit),
            ])
        }

        switch provider {
        case .codex?:
            sections.append(usageSection(for: .codex, titlePrefix: "Codex"))
            sections.append(accountSection(
                preferred: nil,
                codex: store.snapshot(for: .codex),
                preferClaude: false))
        case .claude?:
            let snap = store.snapshot(for: .claude)
            sections.append(usageSection(for: .claude, titlePrefix: "Claude"))
            sections.append(accountSection(
                preferred: snap,
                codex: store.snapshot(for: .codex),
                preferClaude: true))
        case nil:
            var addedUsage = false
            if store.isEnabled(.codex) {
                sections.append(usageSection(for: .codex, titlePrefix: "Codex"))
                addedUsage = true
            }
            if store.isEnabled(.claude) {
                sections.append(usageSection(for: .claude, titlePrefix: "Claude"))
                addedUsage = true
            }
            if addedUsage {
                sections.append(accountSection(
                    preferred: store.snapshot(for: .claude),
                    codex: store.snapshot(for: .codex),
                    preferClaude: store.isEnabled(.claude)))
            } else {
                sections.append(Section(entries: [.text("No usage configured.", .secondary)]))
            }
        }

        sections.append(actionsSection(for: provider))
        sections.append(metaSection())

        return MenuDescriptor(sections: sections)
    }

    private static func capFirst(_ text: String) -> String {
        guard let first = text.unicodeScalars.first else { return text }
        let cappedFirst = String(first).capitalized
        let remainder = String(text.unicodeScalars.dropFirst())
        return cappedFirst + remainder
    }
}
