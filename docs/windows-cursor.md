# Cursor on Windows

Cursor support is preview support. The Windows public preview uses a manual cookie header only.

## Credential Source

Copy the `Cookie:` header from a signed-in request to `https://cursor.com`, then paste it into the Cursor manual cookie field in CodexBar Settings.

This manual cookie header flow is preview-only and may be replaced by a more ergonomic account flow later.

## What CodexBar Reads

CodexBar reads the Cursor manual cookie header from its local settings. It does not import browser cookies automatically in the Windows preview.

## Endpoint Family

CodexBar calls Cursor account and usage endpoint families on `cursor.com`, including usage summary and current account metadata.

## What Is Displayed

The tray popover can show:

- Cursor account email when available
- Included plan usage
- On-demand usage
- Billing cycle reset time when returned by Cursor

## Common Setup Errors

- Missing cookie header: paste the full `Cookie:` header, not an individual token value.
- Expired cookie: sign in to Cursor in your browser and copy a fresh request header.
- Usage data is empty: Cursor may have changed the preview API shape, or the account may not expose the requested usage fields.
- Browser privacy tools remove cookies: copy the header from a normal signed-in Cursor dashboard request.
