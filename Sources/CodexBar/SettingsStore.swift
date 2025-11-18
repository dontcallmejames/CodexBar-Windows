import AppKit
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

    /// When enabled (via debug menu), dump the fetched credits page HTML to /tmp for diagnostics.
    @AppStorage("creditsDebugDump") var creditsDebugDump: Bool = false

    @AppStorage("showCodexUsage") var showCodexUsage: Bool = true
    @AppStorage("showClaudeUsage") var showClaudeUsage: Bool = false

    init(userDefaults: UserDefaults = .standard) {
        let raw = userDefaults.string(forKey: "refreshFrequency") ?? RefreshFrequency.twoMinutes.rawValue
        self.refreshFrequency = RefreshFrequency(rawValue: raw) ?? .twoMinutes
        LaunchAtLoginManager.setEnabled(self.launchAtLogin)

        // If debug flag not set, but a dump is requested via env, honor it.
        if ProcessInfo.processInfo.environment["CODEXBAR_DEBUG_DUMP"] == "1" {
            self.creditsDebugDump = true
            self.debugMenuEnabled = true
        }
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
