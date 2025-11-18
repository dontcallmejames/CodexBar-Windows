import Foundation
import WebKit

struct CreditEvent: Identifiable, Equatable {
    let id = UUID()
    let date: Date
    let service: String
    let creditsUsed: Double
}

struct CreditsSnapshot: Equatable {
    let remaining: Double
    let events: [CreditEvent]
    let updatedAt: Date
}

/// Scrapes the ChatGPT usage page (using the logged-in browser session) to read remaining
/// credits and the credits usage history. This relies on the shared `WKWebsiteDataStore`
/// to reuse the users cookies, which avoids prompting for login inside the menubar app.
@MainActor
struct CreditsFetcher {
    private let usageURL = URL(string: "https://chatgpt.com/codex/settings/usage")!

    func loadLatestCredits(debugDump: Bool = false) async throws -> CreditsSnapshot {
        let config = WKWebViewConfiguration()
        config.websiteDataStore = .default()

        let webView = WKWebView(frame: .zero, configuration: config)
        _ = webView.load(URLRequest(url: self.usageURL))

        try await withCheckedThrowingContinuation { (cont: CheckedContinuation<Void, Error>) in
            let delegate = NavigationDelegate { result in
                cont.resume(with: result)
            }
            webView.navigationDelegate = delegate
            webView.codexNavigationDelegate = delegate
        }

        // The usage page is a SPA; wait for client-rendered content to appear (and for workspace picker dismissal).
        let maxAttempts = 120 // ~60s
        let delay: UInt64 = 500_000_000 // 0.5s
        var lastBody: String?

        for _ in 0..<maxAttempts {
            let result = try await Self.tryParseSnapshot(webView: webView)

            if let snap = result.snapshot {
                if debugDump, let html = result.bodyHTML {
                    Self.writeDebugHTML(html)
                }
                return snap
            }

            if let body = result.bodyText { lastBody = body }

            if result.workspacePicker {
                try? await Task.sleep(nanoseconds: delay)
                continue
            }

            if let href = result.href, !href.contains("/codex/settings/usage") {
                _ = webView.load(URLRequest(url: self.usageURL))
            }

            if debugDump, let html = result.bodyHTML {
                Self.writeDebugHTML(html)
            }

            try? await Task.sleep(nanoseconds: delay)
        }

        throw CreditsError.noCreditsFoundWithBody(lastBody ?? "")
    }

    private struct ScrapeResult {
        let snapshot: CreditsSnapshot?
        let workspacePicker: Bool
        let href: String?
        let bodyText: String?
        let bodyHTML: String?
    }

    private static func tryParseSnapshot(webView: WKWebView) async throws -> ScrapeResult {
        // Try structured scraping first.
        let script = """
        (() => {
          const textOf = el => (el && (el.innerText || el.textContent)) ? String(el.innerText || el.textContent).trim() : '';

          // Credits remaining block: find the first section that mentions it and grab the first pure-number descendant.
          let creditsNumber = null;
          const candidates = Array.from(document.querySelectorAll('*')).filter(el => {
            const t = textOf(el);
            return t && t.includes('Credits remaining');
          });
          for (const el of candidates) {
            const numbers = Array.from(el.querySelectorAll('*'))
              .map(n => textOf(n))
              .filter(txt => /^[0-9][0-9.,]+$/.test(txt));
            if (numbers.length > 0) {
              creditsNumber = numbers[0];
              break;
            }
            const t = textOf(el);
            const m = t.match(/Credits\\s+remaining[^0-9]*([0-9][0-9.,]+)/i);
            if (m && m[1]) { creditsNumber = m[1]; break; }
          }

          // Table rows: assume three columns (Date, Service, Credits used)
          const rows = Array.from(document.querySelectorAll('table tbody tr')).map(tr => {
            const cells = Array.from(tr.querySelectorAll('td')).map(td => textOf(td).trim());
            return cells;
          }).filter(r => r.length >= 3);

          const workspacePicker = Array.from(document.querySelectorAll('*')).some(el => textOf(el).includes('Select a workspace'));
          const href = window.location ? window.location.href : '';

          return { creditsNumber, rows, bodyText: document.body.innerText, bodyHTML: document.documentElement.outerHTML, workspacePicker, href };
        })();
        """
        let resultAny = try await webView.evaluateJavaScript(script)
        guard let dict = resultAny as? [String: Any] else {
            return ScrapeResult(snapshot: nil, workspacePicker: false, href: nil, bodyText: nil, bodyHTML: nil)
        }

        if let picker = dict["workspacePicker"] as? Bool, picker == true {
            // User still needs to pick a workspace; keep waiting.
            return ScrapeResult(snapshot: nil, workspacePicker: true, href: dict["href"] as? String, bodyText: dict["bodyText"] as? String, bodyHTML: dict["bodyHTML"] as? String)
        }
        if let numString = dict["creditsNumber"] as? String,
           let remaining = Double(numString.replacingOccurrences(of: ",", with: "")) {
            let rows = (dict["rows"] as? [[String]]) ?? []
            let events = rows.compactMap { row -> CreditEvent? in
                guard row.count >= 3 else { return nil }
                let dateString = row[0]
                let service = row[1]
                let amountString = row[2]

                let formatter = DateFormatter()
                formatter.locale = Locale(identifier: "en_US_POSIX")
                formatter.dateFormat = "MMM d, yyyy"
                guard let date = formatter.date(from: dateString) else { return nil }
                let creditsUsed = Double(amountString.replacingOccurrences(of: ",", with: "")
                    .replacingOccurrences(of: "credits", with: "")
                    .trimmingCharacters(in: .whitespaces)) ?? 0
                return CreditEvent(date: date, service: service, creditsUsed: creditsUsed)
            }
            let snapshot = CreditsSnapshot(remaining: remaining, events: events, updatedAt: Date())
            return ScrapeResult(snapshot: snapshot, workspacePicker: false, href: dict["href"] as? String, bodyText: dict["bodyText"] as? String, bodyHTML: dict["bodyHTML"] as? String)
        }

        // Fallback to body text parsing.
        if let bodyText = dict["bodyText"] as? String {
            if let remaining = Self.parseRemainingCredits(from: bodyText) {
                let events = Self.parseEvents(from: bodyText)
                let snapshot = CreditsSnapshot(remaining: remaining, events: events, updatedAt: Date())
                return ScrapeResult(snapshot: snapshot, workspacePicker: false, href: dict["href"] as? String, bodyText: dict["bodyText"] as? String, bodyHTML: dict["bodyHTML"] as? String)
            }
        }
        return ScrapeResult(snapshot: nil, workspacePicker: false, href: dict["href"] as? String, bodyText: dict["bodyText"] as? String, bodyHTML: dict["bodyHTML"] as? String)
    }

    private static func writeDebugHTML(_ html: String) {
        let url = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("codex-credits-\(Int(Date().timeIntervalSince1970)).html")
        try? html.write(to: url, atomically: true, encoding: .utf8)
    }

    private static func parseRemainingCredits(from text: String) -> Double? {
        let pattern = "Credits remaining\\s+([0-9.,]+)"
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) else { return nil }
        let range = NSRange(location: 0, length: text.utf16.count)
        guard let match = regex.firstMatch(in: text, options: [], range: range), match.numberOfRanges >= 2 else {
            return nil
        }
        if let valueRange = Range(match.range(at: 1), in: text) {
            let raw = text[valueRange].replacingOccurrences(of: ",", with: "")
            return Double(raw)
        }
        return nil
    }

    private static func parseEvents(from text: String) -> [CreditEvent] {
        // Example row: "Nov 17, 2025\nCLI\n1,448.98 credits"
        let pattern = "([A-Z][a-z]{2} \\d{1,2}, \\d{4})\\s+([A-Za-z ]+)\\s+([0-9.,]+) credits"
        guard let regex = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive, .dotMatchesLineSeparators]) else {
            return []
        }

        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "MMM d, yyyy"

        let range = NSRange(location: 0, length: text.utf16.count)
        let matches = regex.matches(in: text, options: [], range: range)

        return matches.compactMap { match in
            guard match.numberOfRanges >= 4,
                  let dateRange = Range(match.range(at: 1), in: text),
                  let serviceRange = Range(match.range(at: 2), in: text),
                  let creditsRange = Range(match.range(at: 3), in: text),
                  let date = formatter.date(from: String(text[dateRange]))
            else { return nil }

            let rawCredits = String(text[creditsRange]).replacingOccurrences(of: ",", with: "")
            let creditsUsed = Double(rawCredits) ?? 0
            let service = String(text[serviceRange]).trimmingCharacters(in: .whitespacesAndNewlines)
            return CreditEvent(date: date, service: service, creditsUsed: creditsUsed)
        }
    }
}

private enum CreditsError: LocalizedError {
    case noCreditsFound
    case noCreditsFoundWithBody(String)
    case parseFailed

    var errorDescription: String? {
        switch self {
        case .noCreditsFound: "Could not find credits on the usage page."
        case .noCreditsFoundWithBody(let body):
            "Could not find credits on the usage page. Body sample: \(body.prefix(200))"
        case .parseFailed: "Failed to parse credits data."
        }
    }
}

// MARK: - Navigation helper

@MainActor
final class NavigationDelegate: NSObject, WKNavigationDelegate {
    private let completion: (Result<Void, Error>) -> Void
    static var associationKey: UInt8 = 0

    init(completion: @escaping (Result<Void, Error>) -> Void) {
        self.completion = completion
    }

    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        self.completion(.success(()))
    }

    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
        self.completion(.failure(error))
    }

    func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
        self.completion(.failure(error))
    }
}

extension WKWebView {
    var codexNavigationDelegate: NavigationDelegate? {
        get {
            objc_getAssociatedObject(self, &NavigationDelegate.associationKey) as? NavigationDelegate
        }
        set {
            objc_setAssociatedObject(self, &NavigationDelegate.associationKey, newValue, .OBJC_ASSOCIATION_RETAIN_NONATOMIC)
        }
    }
}
