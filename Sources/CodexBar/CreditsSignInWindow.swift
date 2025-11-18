import SwiftUI
import WebKit
import AppKit

/// Lightweight helper that lets the user sign in to ChatGPT within WebKit so
/// the credits scraper can reuse the cookies.
@MainActor
enum CreditsSignInWindow {
    private static var controller: NSWindowController?
    private static var previousActivationPolicy: NSApplication.ActivationPolicy?

    static func present() {
        if let controller, controller.window?.isVisible == true {
            controller.window?.makeKeyAndOrderFront(nil)
            NSApp.activate(ignoringOtherApps: true)
            return
        }

        // Temporarily promote the app to a regular activation policy so the window can become key/front.
        if self.previousActivationPolicy == nil {
            self.previousActivationPolicy = NSApp.activationPolicy()
            if self.previousActivationPolicy != .regular {
                NSApp.setActivationPolicy(.regular)
            }
        }

        let view = CreditsSignInView()
        let hosting = NSHostingController(rootView: view)
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 960, height: 720),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false)
        window.title = "Sign in to ChatGPT"
        window.isReleasedWhenClosed = false
        window.setContentSize(NSSize(width: 1100, height: 800))
        window.minSize = NSSize(width: 900, height: 600)
        window.contentViewController = hosting
        window.center()
        let controller = NSWindowController(window: window)
        self.controller = controller
        controller.showWindow(nil)
        NSApp.activate(ignoringOtherApps: true)
        controller.window?.deminiaturize(nil)
        controller.window?.makeKeyAndOrderFront(nil)
        controller.window?.orderFrontRegardless()
    }

    static func dismiss() {
        self.controller?.close()
        self.controller = nil

        if let policy = self.previousActivationPolicy {
            NSApp.setActivationPolicy(policy)
            self.previousActivationPolicy = nil
        }
    }
}

private struct CreditsSignInView: View {
    private let usageURL = URL(string: "https://chatgpt.com/codex/settings/usage")!
    @State private var webView = WKWebView(frame: .zero, configuration: CreditsSignInView.makeConfiguration())

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text("Sign in with your ChatGPT account to allow CodexBar to read credits.")
                    .font(.headline)
                Spacer()
                Button("Reload") { load() }
                Button("Close") { self.closeWindow() }
            }
            .padding(10)
            Divider()
            WebViewContainer(webView: self.webView)
                .onAppear { load() }
        }
    }

    private func load() {
        let request = URLRequest(url: self.usageURL)
        self.webView.load(request)
    }

    private func closeWindow() {
        self.webView.stopLoading()
        self.webView.navigationDelegate = nil
        self.webView.uiDelegate = nil
        self.webView.codexNavigationDelegate = nil
        self.webView = WKWebView(frame: .zero, configuration: CreditsSignInView.makeConfiguration())
        CreditsSignInWindow.dismiss()
    }

    private static func makeConfiguration() -> WKWebViewConfiguration {
        let config = WKWebViewConfiguration()
        config.websiteDataStore = .default()
        return config
    }
}

private struct WebViewContainer: NSViewRepresentable {
    let webView: WKWebView

    func makeNSView(context: Context) -> WKWebView { self.webView }
    func updateNSView(_ nsView: WKWebView, context: Context) {}
}
