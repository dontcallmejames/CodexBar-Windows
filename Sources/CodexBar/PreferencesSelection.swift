import Foundation

@MainActor
final class PreferencesSelection: ObservableObject {
    @Published var tab: PreferencesTab = .general
}
