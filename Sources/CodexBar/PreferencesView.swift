import SwiftUI
import AppKit

enum PreferencesTab: String, Hashable {
    case general
    case about
}

@MainActor
struct PreferencesView: View {
    @ObservedObject var settings: SettingsStore
    @ObservedObject var store: UsageStore
    let updater: UpdaterProviding
    @ObservedObject var selection: PreferencesSelection

    var body: some View {
        TabView(selection: self.$selection.tab) {
            GeneralPane(settings: self.settings, store: self.store)
                .tabItem { Label("General", systemImage: "gearshape") }
                .tag(PreferencesTab.general)

            AboutPane(updater: self.updater)
                .tabItem { Label("About", systemImage: "info.circle") }
                .tag(PreferencesTab.about)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 12)
        .frame(minWidth: 560, idealWidth: 560, maxWidth: 560, minHeight: 520, idealHeight: 520)
    }
}

// MARK: - General

@MainActor
private struct GeneralPane: View {
    @ObservedObject var settings: SettingsStore
    @ObservedObject var store: UsageStore

    var body: some View {
        Form {
            Section {
                PreferenceToggleRow(
                    title: "Show Codex usage",
                    subtitle: "Display the Codex rate limits in the menu bar.",
                    binding: self.codexBinding)

                PreferenceToggleRow(
                    title: "Show Claude usage",
                    subtitle: "Display Claude limits if available.",
                    binding: self.claudeBinding)

                PreferenceToggleRow(
                    title: "Launch at login",
                    subtitle: "Start CodexBar automatically when you log in.",
                    binding: self.$settings.launchAtLogin)
            }

            Section("Refresh every") {
                Picker("Refresh every", selection: self.$settings.refreshFrequency) {
                    ForEach(RefreshFrequency.allCases) { option in
                        Text(option.label).tag(option)
                    }
                }
                .pickerStyle(.segmented)
                if self.settings.refreshFrequency == .manual {
                    Text("Auto-refresh is off; use the menu’s Refresh.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }
            }

            Section("Credits & auth") {
                if store.credits == nil {
                    Text("Sign in once to show credits usage.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                    Button("Sign in to fetch credits…") { CreditsSignInWindow.present() }
                }
                Button("Log out / clear cookies") {
                    Task { await self.store.clearCookies() }
                }
            }

            if self.settings.debugMenuEnabled {
                Section("Debug") {
                    PreferenceToggleRow(
                        title: "Dump credits HTML to /tmp",
                        subtitle: "For diagnostics only.",
                        binding: self.$settings.creditsDebugDump)
                    Button("Replay loading animation") {
                        NotificationCenter.default.post(name: .codexbarDebugReplayAllAnimations, object: nil)
                        self.store.replayLoadingAnimation()
                    }
                    Button("Dump Claude probe output") {
                        Task { await self.store.debugDumpClaude() }
                    }
                }
            }
        }
        .formStyle(.grouped)
    }

    private var codexBinding: Binding<Bool> {
        Binding(
            get: { self.settings.showCodexUsage },
            set: { newValue in
                self.settings.showCodexUsage = newValue
                self.settings.ensureAtLeastOneProviderVisible()
            })
    }

    private var claudeBinding: Binding<Bool> {
        Binding(
            get: { self.settings.showClaudeUsage },
            set: { newValue in
                self.settings.showClaudeUsage = newValue
                self.settings.ensureAtLeastOneProviderVisible()
            })
    }
}

// MARK: - About

@MainActor
private struct AboutPane: View {
    let updater: UpdaterProviding
    @State private var iconHover = false

    private var versionString: String {
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "–"
        let build = Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String
        return build.map { "\(version) (\($0))" } ?? version
    }

    var body: some View {
        VStack(spacing: 16) {
            if let image = NSApplication.shared.applicationIconImage {
                Image(nsImage: image)
                    .resizable()
                    .frame(width: 96, height: 96)
                    .cornerRadius(16)
                    .scaleEffect(self.iconHover ? 1.04 : 1.0)
                    .shadow(color: self.iconHover ? .accentColor.opacity(0.25) : .clear, radius: 6)
                    .onHover { self.iconHover = $0 }
            }

            VStack(spacing: 2) {
                Text("CodexBar")
                    .font(.title3).bold()
                Text("Version \(self.versionString)")
                    .foregroundStyle(.secondary)
                Text("Keep Codex usage visible—menu bar first.")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }

            VStack(alignment: .center, spacing: 8) {
                AboutLinkRow(icon: "chevron.left.slash.chevron.right", title: "GitHub", url: "https://github.com/steipete/CodexBar")
                AboutLinkRow(icon: "globe", title: "Website", url: "https://steipete.me")
                AboutLinkRow(icon: "bird", title: "Twitter", url: "https://twitter.com/steipete")
                AboutLinkRow(icon: "envelope", title: "Email", url: "mailto:peter@steipete.me")
            }
            .frame(maxWidth: .infinity)

            Divider()

            if updater.isAvailable {
                VStack(spacing: 10) {
                    Toggle("Check for updates automatically", isOn: Binding(
                        get: { updater.automaticallyChecksForUpdates },
                        set: { updater.automaticallyChecksForUpdates = $0 }))
                        .toggleStyle(.checkbox)
                    Button("Check for Updates…") { updater.checkForUpdates(nil) }
                }
            } else {
                Text("Updates unavailable in this build.")
                    .foregroundStyle(.secondary)
            }

            Text("© 2025 Peter Steinberger. MIT License.")
                .font(.footnote)
                .foregroundStyle(.secondary)
                .padding(.top, 4)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(.top, 16)
        .padding(.horizontal, 24)
        .padding(.bottom, 24)
    }
}

// MARK: - Reusable rows

@MainActor
private struct PreferenceToggleRow: View {
    let title: String
    let subtitle: String
    @Binding var binding: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Toggle(isOn: self.$binding) {
                Text(self.title)
                    .font(.body)
            }
            .toggleStyle(.checkbox)

            Text(self.subtitle)
                .font(.footnote)
                .foregroundStyle(.tertiary)
                .fixedSize(horizontal: false, vertical: true)
        }
    }
}

@MainActor
private struct AboutLinkRow: View {
    let icon: String
    let title: String
    let url: String

    var body: some View {
        Button {
            if let url = URL(string: self.url) { NSWorkspace.shared.open(url) }
        } label: {
            HStack(spacing: 8) {
                Image(systemName: self.icon)
                Text(self.title)
                Spacer()
            }
            .frame(maxWidth: .infinity)
        }
        .buttonStyle(.plain)
        .contentShape(Rectangle())
    }
}
