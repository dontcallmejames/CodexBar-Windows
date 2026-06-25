# Antigravity (Windows)

CodexBar reads Claude, Gemini Pro, and Gemini Flash quota from the local Antigravity
language server. This replaces the Gemini CLI path, which Google retired on June 18, 2026.

## Requirements

- Install Antigravity (the `agy` CLI or the Antigravity IDE) and sign in with your Google account.
- Antigravity must be **running** for CodexBar to read quota. The CLI's language server runs
  while `agy` is active; the IDE's runs while the IDE is open.

## How it works

CodexBar finds the running Antigravity language server on a loopback port and calls its local
quota RPC. No tokens are stored by CodexBar; for the IDE it reads the CSRF token from the running
process, and the `agy` CLI requires no token.

## Troubleshooting

- **"Antigravity isn't running."** — Start `agy` or the Antigravity IDE, then refresh.
- **"Antigravity isn't available."** — The server was found but did not return quota. Make sure
  you are signed in (`agy` login), then refresh.
- **"Limits not available."** — You are signed in but the server reported no quota buckets yet.
  Use Antigravity once, then refresh.
