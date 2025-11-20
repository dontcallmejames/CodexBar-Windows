import AppKit
import Combine
import ServiceManagement
import SwiftUI

enum RefreshFrequency: String, CaseIterable, Identifiable {
    case manual
    case oneMinute
    case twoMinutes
    case fiveMinutes

    var id: String { self.rawValue }

    var seconds: TimeInterval? {
        switch self {
        case .manual: nil
        case .oneMinute: 60
        case .twoMinutes: 120
        case .fiveMinutes: 300
        }
    }

    var label: String {
        switch self {
        case .manual: "Manual"
        case .oneMinute: "1 min"
        case .twoMinutes: "2 min"
        case .fiveMinutes: "5 min"
        }
    }
}

@MainActor
final class SettingsStore: ObservableObject {
    @Published var refreshFrequency: RefreshFrequency {
        didSet { UserDefaults.standard.set(self.refreshFrequency.rawValue, forKey: "refreshFrequency") }
    }

    @AppStorage("launchAtLogin") var launchAtLogin: Bool = false {
        didSet { LaunchAtLoginManager.setEnabled(self.launchAtLogin) }
    }

    /// Hidden toggle to reveal debug-only menu items (enable via defaults write com.steipete.CodexBar debugMenuEnabled
    /// -bool YES).
    @AppStorage("debugMenuEnabled") var debugMenuEnabled: Bool = false

    @AppStorage("debugLoadingPattern") private var debugLoadingPatternRaw: String?

    /// Optional override for the loading animation pattern, exposed via the Debug tab.
    var debugLoadingPattern: LoadingPattern? {
        get { self.debugLoadingPatternRaw.flatMap(LoadingPattern.init(rawValue:)) }
        set {
            self.objectWillChange.send()
            self.debugLoadingPatternRaw = newValue?.rawValue
        }
    }

    private static let providerToggleKey = "providerToggles"
    private var providerToggles: [String: Bool]

    init(userDefaults: UserDefaults = .standard) {
        let raw = userDefaults.string(forKey: "refreshFrequency") ?? RefreshFrequency.twoMinutes.rawValue
        self.refreshFrequency = RefreshFrequency(rawValue: raw) ?? .twoMinutes
        if let dict = userDefaults.dictionary(forKey: Self.providerToggleKey) as? [String: Bool] {
            self.providerToggles = dict
        } else {
            // Defaults: Codex on, Claude off.
            self.providerToggles = ["codex": true, "claude": false]
        }

        userDefaults.set(self.providerToggles, forKey: Self.providerToggleKey)
        // Purge legacy keys since we never shipped them.
        userDefaults.removeObject(forKey: "showCodexUsage")
        userDefaults.removeObject(forKey: "showClaudeUsage")
        LaunchAtLoginManager.setEnabled(self.launchAtLogin)
    }

    func isProviderEnabled(provider: UsageProvider, metadata: ProviderMetadata) -> Bool {
        self.providerToggles[metadata.cliName] ?? metadata.defaultEnabled
    }

    func setProviderEnabled(provider: UsageProvider, metadata: ProviderMetadata, enabled: Bool) {
        self.objectWillChange.send()
        self.providerToggles[metadata.cliName] = enabled
        UserDefaults.standard.set(self.providerToggles, forKey: Self.providerToggleKey)
    }
}

enum LaunchAtLoginManager {
    @MainActor
    static func setEnabled(_ enabled: Bool) {
        guard #available(macOS 13, *) else { return }
        let service = SMAppService.mainApp
        if enabled {
            try? service.register()
        } else {
            try? service.unregister()
        }
    }
}
