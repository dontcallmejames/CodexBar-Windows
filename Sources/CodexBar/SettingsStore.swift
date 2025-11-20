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

    @AppStorage("showCodexUsage") var showCodexUsage: Bool = true
    @AppStorage("showClaudeUsage") var showClaudeUsage: Bool = false

    init(userDefaults: UserDefaults = .standard) {
        let raw = userDefaults.string(forKey: "refreshFrequency") ?? RefreshFrequency.twoMinutes.rawValue
        self.refreshFrequency = RefreshFrequency(rawValue: raw) ?? .twoMinutes
        LaunchAtLoginManager.setEnabled(self.launchAtLogin)

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
