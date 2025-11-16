import AppKit

@MainActor
func showAbout() {
    NSApp.activate(ignoringOtherApps: true)

    let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "–"
    let build = Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? ""
    let versionString = build.isEmpty ? version : "\(version) (\(build))"

    let separator = NSAttributedString(string: " · ", attributes: [
        .font: NSFont.systemFont(ofSize: NSFont.smallSystemFontSize),
    ])

    func makeLink(_ title: String, urlString: String) -> NSAttributedString {
        NSAttributedString(string: title, attributes: [
            .link: URL(string: urlString) as Any,
            .font: NSFont.systemFont(ofSize: NSFont.smallSystemFontSize),
        ])
    }

    let credits = NSMutableAttributedString(string: "Peter Steinberger — MIT License\n")
    credits.append(makeLink("GitHub", urlString: "https://github.com/steipete/CodexBar"))
    credits.append(separator)
    credits.append(makeLink("Website", urlString: "https://steipete.me"))
    credits.append(separator)
    credits.append(makeLink("Twitter", urlString: "https://twitter.com/steipete"))
    credits.append(separator)
    credits.append(makeLink("Email", urlString: "mailto:peter@steipete.me"))

    let options: [NSApplication.AboutPanelOptionKey: Any] = [
        .applicationName: "CodexBar",
        .applicationVersion: versionString,
        .version: versionString,
        .credits: credits,
        .applicationIcon: (NSApplication.shared.applicationIconImage ?? NSImage()) as Any,
    ]

    NSApp.orderFrontStandardAboutPanel(options: options)
}
