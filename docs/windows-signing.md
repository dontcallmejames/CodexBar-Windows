# Windows Signing

CodexBar releases can be signed via [Azure Trusted Signing](https://learn.microsoft.com/azure/trusted-signing/) — Microsoft's managed signing service backed by an HSM in Azure. Unsigned local builds remain supported for development.

Trusted Signing-signed binaries are chained to a Microsoft-issued certificate, so they are recognized by Authenticode and (over time, as download reputation builds) by Microsoft Defender SmartScreen.

Signed releases sign both:

- `CodexBar.WinUI.exe` inside the portable zip.
- `CodexBar-Windows-*.installer.exe` after Inno Setup builds the installer.

## Azure-Side Setup (one-time)

You only need to do this once. Identity verification can take 3–5 business days.

1. **Azure subscription.** Sign in at <https://portal.azure.com> with a Microsoft account. If you don't have a subscription, create a Pay-As-You-Go one — Trusted Signing is billed at roughly $9.99 USD/month per certificate profile.
2. **Register the resource provider.** Subscriptions → your subscription → Resource providers → search for `Microsoft.CodeSigning` → click Register.
3. **Create a Trusted Signing Account.** Portal → search "Trusted Signing Accounts" → Create. Pick a resource group, region (e.g. `eastus`), and SKU `Basic`. The account name becomes the value of `TRUSTED_SIGNING_ACCOUNT_NAME`.
4. **Create an Identity Validation.** Inside the account → Identity validations → New → Public Trust → Individual (for a personal project). Submit your government ID and proof of address. Wait for approval (typically 3–5 business days).
5. **Create a Certificate Profile.** Inside the account → Certificate profiles → New → choose the validated identity. The profile name becomes `TRUSTED_SIGNING_PROFILE_NAME`.
6. **Note the signing endpoint.** Account overview shows the region endpoint (e.g. `https://eus.codesigning.azure.net`). That URL is `TRUSTED_SIGNING_ENDPOINT`.

## GitHub OIDC Federation (one-time)

GitHub Actions authenticates to Azure via OpenID Connect — no client secret needed.

1. **App registration.** Microsoft Entra ID → App registrations → New registration. Name: `codexbar-trusted-signing-github`. Single tenant. No redirect URI. Note the **Application (client) ID** and **Directory (tenant) ID**.
2. **Federated credential.** Inside the app registration → Certificates & secrets → Federated credentials → Add → "GitHub Actions deploying Azure resources":
   - Organization: `dontcallmejames`
   - Repository: `CodexBar-Windows`
   - Entity type: `Tag`
   - Tag pattern: `v*`
   - Name: `github-tag-releases`

   (Add a second federated credential with Entity type `Branch` and pattern `main` if you want to test signing on `workflow_dispatch` runs.)
3. **Role assignment.** Trusted Signing Account → Access control (IAM) → Add role assignment → role `Trusted Signing Certificate Profile Signer` → assign to the app registration created above.

## GitHub Repository Variables

Once Azure is set up, add these to **Settings → Secrets and variables → Actions → Variables**:

| Name | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | App registration's Application (client) ID |
| `AZURE_TENANT_ID` | Directory (tenant) ID |
| `AZURE_SUBSCRIPTION_ID` | Subscription containing the Trusted Signing Account |
| `TRUSTED_SIGNING_ENDPOINT` | e.g. `https://eus.codesigning.azure.net` |
| `TRUSTED_SIGNING_ACCOUNT_NAME` | Trusted Signing Account name |
| `TRUSTED_SIGNING_PROFILE_NAME` | Certificate profile name |

These are stored as variables (not secrets) — they are not sensitive on their own; the OIDC federation is the access control. No PFX file or password is required anywhere.

## How CI Uses It

The `package` job in `.github/workflows/windows.yml`:

1. Builds the portable zip and installer with `-SkipSigning`.
2. Checks whether all six Trusted Signing variables are set. If any are missing, it logs a notice and publishes the unsigned assets — releases never fail due to missing signing config.
3. When configured: logs in via `azure/login@v2` (OIDC), unzips the portable, signs `CodexBar.WinUI.exe` with `azure/trusted-signing-action`, re-zips, and refreshes the zip checksum.
4. Signs the installer in place, then refreshes its checksum.
5. Uploads + publishes the GitHub Release.

The script-level `-SigningCertificatePath` / `-SigningCertificatePassword` parameters remain for local PFX-based signing if you ever need that path; they are unused by CI.

## Local Signing (PFX fallback)

For local testing with a self-signed or commercial PFX:

```powershell
.\Scripts\package-windows-installer.ps1 `
  -DotNet dotnet `
  -SigningCertificatePath C:\path\to\certificate.pfx `
  -SigningCertificatePassword $env:CODEXBAR_SIGNING_CERTIFICATE_PASSWORD
```

`signtool.exe` is discovered from the Windows SDK; pass `-SignTool` to override.

## Unsigned Builds

If no signing is configured, builds still complete:

```powershell
.\Scripts\package-windows-installer.ps1 -DotNet dotnet -SkipSigning
```

## Verification

After a signed CI run, download the assets and verify:

```powershell
Get-AuthenticodeSignature .\CodexBar-Windows-0.25-win-x64.installer.exe
Expand-Archive .\CodexBar-Windows-0.25-win-x64.zip -DestinationPath .\portable
Get-AuthenticodeSignature .\portable\CodexBar.WinUI.exe
```

Expected: `Status` is `Valid` and the signer chain ends at the Microsoft Identity Verification Root.
