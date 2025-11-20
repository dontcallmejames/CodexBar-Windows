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
        account: AccountInfo
    ) -> MenuDescriptor {
        var sections: [Section] = []

        func codexSection() -> Section {
            var entries: [Entry] = []
            entries.append(.text("Codex · 5h limit", .headline))
            if let snap = store.snapshot(for: .codex) {
                entries.append(.text(UsageFormatter.usageLine(remaining: snap.primary.remainingPercent, used: snap.primary.usedPercent), .primary))
                if let reset = snap.primary.resetDescription { entries.append(.text("Resets \(reset)", .secondary)) }
                entries.append(.text("Codex · Weekly limit", .headline))
                entries.append(.text(UsageFormatter.usageLine(remaining: snap.secondary.remainingPercent, used: snap.secondary.usedPercent), .primary))
                if let reset = snap.secondary.resetDescription { entries.append(.text("Resets \(reset)", .secondary)) }
                entries.append(.text(UsageFormatter.updatedString(from: snap.updatedAt), .secondary))
            } else {
                entries.append(.text("No usage yet", .secondary))
                if let err = store.lastCodexError, !err.isEmpty {
                    entries.append(.action(err, .copyError(err)))
                }
            }

            if let credits = store.credits {
                entries.append(.text("Credits: \(UsageFormatter.creditsString(from: credits.remaining))", .primary))
                if let latest = credits.events.first {
                    entries.append(.text("Last spend: \(UsageFormatter.creditEventSummary(latest))", .secondary))
                }
            } else {
                entries.append(.text("Credits: sign in", .secondary))
            }
            return Section(entries: entries)
        }

        func claudeSection() -> Section {
            var entries: [Entry] = []
            entries.append(.text("Claude · Session", .headline))
            if let snap = store.snapshot(for: .claude) {
                entries.append(.text(UsageFormatter.usageLine(remaining: snap.primary.remainingPercent, used: snap.primary.usedPercent), .primary))
                if let reset = snap.primary.resetDescription { entries.append(.text("Resets \(reset)", .secondary)) }
                entries.append(.text("Claude · Weekly", .headline))
                entries.append(.text(UsageFormatter.usageLine(remaining: snap.secondary.remainingPercent, used: snap.secondary.usedPercent), .primary))
                if let reset = snap.secondary.resetDescription { entries.append(.text("Resets \(reset)", .secondary)) }
                if let opus = snap.tertiary {
                    entries.append(.text("Claude · Opus", .headline))
                    entries.append(.text(UsageFormatter.usageLine(remaining: opus.remainingPercent, used: opus.usedPercent), .primary))
                    if let reset = opus.resetDescription { entries.append(.text("Resets \(reset)", .secondary)) }
                }
                entries.append(.text(UsageFormatter.updatedString(from: snap.updatedAt), .secondary))
                if let email = snap.accountEmail {
                    if let plan = snap.loginMethod, !plan.isEmpty {
                        entries.append(.text("Account: \(email) — \(plan)", .secondary))
                    } else {
                        entries.append(.text("Account: \(email)", .secondary))
                    }
                }
                if let org = snap.accountOrganization, !org.isEmpty { entries.append(.text("Org: \(org)", .secondary)) }
            } else {
                entries.append(.text("No usage yet", .secondary))
                if let err = store.lastClaudeError, !err.isEmpty {
                    entries.append(.action(err, .copyError(err)))
                }
            }
            return Section(entries: entries)
        }

        /// Builds the account section.
        /// - If a Claude snapshot is provided, we only trust its account fields (email/loginMethod).
        ///   This prevents leaking Codex plan/email when the user is looking at Claude.
        /// - If no Claude snapshot is available, fall back to Codex auth info.
        func accountSection(preferred claude: UsageSnapshot?, preferClaude: Bool) -> Section {
            var entries: [Entry] = []
            let emailFromClaude = claude?.accountEmail
            let planFromClaude = claude?.loginMethod

            // Email: Claude wins when requested; otherwise Codex auth.
            let emailText: String = {
                if preferClaude, let e = emailFromClaude, !e.isEmpty { return e }
                if let codexEmail = account.email, !codexEmail.isEmpty { return codexEmail }
                if let e = emailFromClaude, !e.isEmpty { return e }
                return "Unknown"
            }()
            entries.append(.text("Account: \(emailText)", .secondary))

            // Plan: show only Claude plan when in Claude mode; otherwise Codex plan.
            if preferClaude {
                if let plan = planFromClaude, !plan.isEmpty {
                    entries.append(.text("Plan: \(plan)", .secondary))
                }
            } else if let plan = account.plan, !plan.isEmpty {
                entries.append(.text("Plan: \(plan)", .secondary))
            }
            return Section(entries: entries)
        }

        func actionsSection() -> Section {
            Section(entries: [
                .action("Refresh now", .refresh),
                .action("Usage Dashboard", .dashboard)
            ])
        }

        func metaSection() -> Section {
            Section(entries: [
                .action("Settings...", .settings),
                .action("About CodexBar", .about),
                .action("Quit", .quit)
            ])
        }

        switch provider {
        case .codex?:
            sections.append(codexSection())
            sections.append(accountSection(preferred: nil, preferClaude: false))
        case .claude?:
            let claudeSnap = store.snapshot(for: .claude)
            sections.append(claudeSection())
            sections.append(accountSection(preferred: claudeSnap, preferClaude: true))
        case nil:
            var hasUsageSection = false
            if settings.showCodexUsage {
                sections.append(codexSection())
                hasUsageSection = true
            }
            if settings.showClaudeUsage {
                sections.append(claudeSection())
                hasUsageSection = true
            }
            if hasUsageSection {
                let claudeSnap = settings.showClaudeUsage ? store.snapshot(for: .claude) : nil
                sections.append(accountSection(preferred: claudeSnap, preferClaude: settings.showClaudeUsage))
            } else {
                sections.append(Section(entries: [.text("No usage configured.", .secondary)]))
            }
        }

        sections.append(actionsSection())
        sections.append(metaSection())

        return MenuDescriptor(sections: sections)
    }
}
