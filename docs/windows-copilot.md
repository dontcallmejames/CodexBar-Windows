# GitHub Copilot on Windows

CodexBar reads GitHub Copilot usage by calling the `copilot_internal/user` endpoint with the token managed by the GitHub CLI.

## Credential Source

CodexBar shells out to `gh auth token` to obtain a GitHub PAT. The token never touches disk in CodexBar — it is read on demand and held in memory for one request.

Sign in once before enabling Copilot:

```
gh auth login
gh auth status
```

`gh auth status` should print your username and a `Logged in to github.com` line.

## What CodexBar Reads

The app calls `GET https://api.github.com/copilot_internal/user` with the editor-spoofing headers GitHub requires for that route:

- `Authorization: token <github_pat>`
- `Editor-Version: vscode/<version>`
- `Editor-Plugin-Version: copilot-chat/<version>`
- `User-Agent: GitHubCopilotChat/<version>`
- `X-Github-Api-Version: 2025-04-01`

No project files, repos, or chat history are read.

## What Is Displayed

The tray popover shows:

- Copilot plan (`Pro`, `Business`, `Enterprise`, `Free`, ...)
- For paid plans: percentage used of premium interactions and chat
- For the free tier: percentage used of monthly chat and completions quotas
- Reset date when the API returns one

## Common Setup Errors

- `gh` not on PATH: install the GitHub CLI from <https://cli.github.com>.
- `Run `gh auth login` to sign in to GitHub`: the CLI has no active session — sign in and re-enable.
- 401 / 403 from GitHub: your account does not have Copilot access, or the token was revoked. Re-run `gh auth login`.
- Copilot is off by default: opt in from Settings > Providers > Copilot once `gh auth status` reports success.
