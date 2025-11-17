import AppKit
import Sparkle
import SwiftUI

@MainActor
struct UsageRow: View {
    let title: String
    let window: RateWindow

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(self.title).font(.headline)
            Text(UsageFormatter.usageLine(
                remaining: self.window.remainingPercent,
                used: self.window.usedPercent))
            if let reset = window.resetsAt {
                Text("Resets \(reset.formatted(date: .abbreviated, time: .shortened))")
            }
        }
    }
}

@MainActor
struct MenuContent: View {
    @ObservedObject var store: UsageStore
    @ObservedObject var settings: SettingsStore
    let account: AccountInfo
    let updater: SPUStandardUpdaterController

    private var autoUpdateBinding: Binding<Bool> {
        Binding(
            get: { self.updater.updater.automaticallyChecksForUpdates },
            set: { self.updater.updater.automaticallyChecksForUpdates = $0 })
    }

    private var snapshot: UsageSnapshot? { self.store.snapshot }

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            if let snapshot {
                UsageRow(title: "5h limit", window: snapshot.primary)
                UsageRow(title: "Weekly limit", window: snapshot.secondary)
                Text(UsageFormatter.updatedString(from: snapshot.updatedAt))
                    .foregroundStyle(.secondary)
            } else {
                Text("No usage yet").foregroundStyle(.secondary)
                if let error = store.lastError { Text(error).font(.caption) }
            }

            Divider()
            if let email = account.email {
                Text("Account: \(email)")
                    .foregroundStyle(.secondary)
            } else {
                Text("Account: unknown")
                    .foregroundStyle(.secondary)
            }
            if let plan = account.plan {
                Text("Plan: \(plan.capitalized)")
                    .foregroundStyle(.secondary)
            }

            Button {
                Task { await self.store.refresh() }
            } label: {
                Text(self.store.isRefreshing ? "Refreshing…" : "Refresh now")
            }
            .disabled(self.store.isRefreshing)
            .buttonStyle(.plain)
            Button("Usage Dashboard") {
                if let url = URL(string: "https://chatgpt.com/codex/settings/usage") {
                    NSWorkspace.shared.open(url)
                }
            }
            .buttonStyle(.plain)
            Divider()
            Menu("Settings") {
                Menu("Refresh every: \(self.settings.refreshFrequency.label)") {
                    ForEach(RefreshFrequency.allCases) { option in
                        Button {
                            self.settings.refreshFrequency = option
                        } label: {
                            if self.settings.refreshFrequency == option {
                                Label(option.label, systemImage: "checkmark")
                            } else {
                                Text(option.label)
                            }
                        }
                    }
                }
                Toggle("Automatically check for updates", isOn: self.autoUpdateBinding)
                Toggle("Launch at login", isOn: self.$settings.launchAtLogin)
                Button("Check for Updates…") {
                    self.updater.checkForUpdates(nil)
                }
                if self.settings.debugMenuEnabled {
                    Divider()
                    Button("Debug: Replay Loading Animation") {
                        NotificationCenter.default.post(name: .codexbarDebugReplayAllAnimations, object: nil)
                        self.store.replayLoadingAnimation()
                    }
                }
            }
            .buttonStyle(.plain)
            Button("About CodexBar") {
                showAbout()
            }
            .buttonStyle(.plain)
            Button("Quit") { NSApp.terminate(nil) }
                .buttonStyle(.plain)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
        .frame(minWidth: 240, alignment: .leading)
        .foregroundStyle(.primary)
        if self.settings.refreshFrequency == .manual {
            Text("Auto-refresh is off")
                .font(.caption2)
                .foregroundStyle(.secondary)
                .padding(.horizontal, 10)
        }
    }

    private func relativeUpdated(at date: Date) -> String {
        let now = Date()
        if let hours = Calendar.current.dateComponents([.hour], from: date, to: now).hour, hours < 24 {
            let rel = RelativeDateTimeFormatter()
            rel.unitsStyle = .abbreviated
            return "Updated \(rel.localizedString(for: date, relativeTo: now))"
        } else {
            return "Updated \(date.formatted(date: .omitted, time: .shortened))"
        }
    }
}
