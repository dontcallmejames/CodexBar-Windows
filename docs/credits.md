# Credits display in CodexBar

This feature shows ChatGPT “credits remaining” and a short usage history directly in the menu bar. Because OpenAI has not exposed a public API for credits, CodexBar scrapes the signed‑in ChatGPT Usage page (`https://chatgpt.com/codex/settings/usage`) with a `WKWebView` that reuses the system WebKit cookie store.

## What gets scraped
- **Credits remaining**: the numeric value next to “Credits remaining”.
- **Credits usage history**: the table rows under “Credits usage history” (date, service, credits used). We currently show the most recent few rows in the menu.

## How it works
1. The app spins up a headless `WKWebView` with `WKWebsiteDataStore.default()` so it can reuse cookies created by WebKit.
2. It loads the Usage URL and waits for navigation to finish.
3. It reads `document.body.innerText` and parses the credits number plus rows in the history table using regexes.
4. Parsed data is stored in `CreditsSnapshot` and rendered in the menu alongside rate‑limit usage.

## Sign‑in requirement (important)
`WKWebsiteDataStore.default()` only sees WebKit cookies. Being signed in to ChatGPT in Chrome does **not** make you signed in for WebKit. If the scraper cannot find “Credits remaining”, it’s because the page loaded an unauthenticated or Cloudflare challenge shell.

Ways to seed cookies for WebKit:
- **Preferred:** Click “Sign in to fetch credits…” in the CodexBar menu. This opens an in-app WebKit window on the Usage page; sign in there once, close it, then press “Refresh now”.
- Or open `https://chatgpt.com/codex/settings/usage` in Safari (WebKit) and sign in, then press “Refresh now” in CodexBar.

If you still see “Credits unavailable: Could not find credits on the usage page”, cookies aren’t available or the page was challenged.

## Cloudflare challenges
Occasionally Cloudflare presents a challenge page. In that case, open the Usage URL in Safari, solve the challenge, and refresh again from CodexBar. Consider adding a visible in‑app sign‑in helper if this becomes frequent.

## Privacy / data handled
- We only read text from the Usage page; no data is sent anywhere outside your machine.
- Parsed values are kept in memory; no on‑disk cache is written by CodexBar (the browser cache/cookies live in WebKit’s store as usual).

## Current limitations
- Locale: parsing expects English strings like “Credits remaining” and dates like “Nov 17, 2025”. Non‑English locales will need parser updates.
- Browser silo: only WebKit cookies work; Chrome/Firefox sessions are invisible to WebKit.
- No API fallback yet; if OpenAI publishes a credits API we should switch to that.

## Developer notes
- Scraper lives in `Sources/CodexBar/CreditsFetcher.swift` and runs inside `UsageStore.refresh()` alongside the existing rate‑limit fetch.
- UI rendering is in `MenuContent` using helpers in `UsageFormatter`.
- If you add a visible sign‑in helper, prefer a small WebView window that loads the Usage page and closes itself once navigation completes successfully.
