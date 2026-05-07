# Gemini on Windows

Gemini support is preview support. The Windows public preview requires Gemini CLI OAuth credentials.

## Credential Source

CodexBar checks `%USERPROFILE%\.gemini\oauth_creds.json`. Sign in with the Gemini CLI using OAuth before enabling Gemini in CodexBar.

API key and Vertex AI Gemini CLI modes are not supported for preview usage display.

## What CodexBar Reads

The app reads the Gemini CLI OAuth access token, refresh token, ID token, and expiry timestamp from `oauth_creds.json`. The ID token is used to display the account email when the claim is available.

## Endpoint Family

CodexBar calls Google Gemini Code Assist quota endpoint families with the local OAuth access token. If the token is expired and a refresh token is available, it uses Google's OAuth token endpoint to refresh credentials.

## What Is Displayed

The tray popover can show:

- Gemini account email when available
- Plan family such as Free, Paid, Workspace, or Legacy when returned by the quota response
- Pro model usage
- Flash model usage
- Reset times for quota buckets when returned by Gemini

## Common Setup Errors

- `oauth_creds.json` is missing: run Gemini CLI OAuth sign-in.
- API key or Vertex AI mode is selected: switch Gemini CLI to OAuth for preview support.
- Token refresh fails: update or reinstall the Gemini CLI, then sign in again.
- No quota buckets appear: Gemini may have changed the preview quota API response shape.
