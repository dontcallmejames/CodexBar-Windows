# Windows Signing

CodexBar can build unsigned local packages and installers by default. Public Windows releases can be signed when a code-signing PFX certificate is available.

Signing reduces install friction and helps users identify the publisher. It does not immediately remove every Microsoft Defender SmartScreen warning; SmartScreen reputation builds over time for the certificate and downloaded file.

Signed releases sign both:

- `CodexBar.WinApp.exe` before the portable zip is created.
- `CodexBar-Windows-*.installer.exe` after Inno Setup creates the installer.

## Local Signing

Build and sign the portable package plus installer:

```powershell
.\Scripts\package-windows-installer.ps1 `
  -DotNet dotnet `
  -SigningCertificatePath C:\path\to\certificate.pfx `
  -SigningCertificatePassword $env:CODEXBAR_SIGNING_CERTIFICATE_PASSWORD
```

The package and installer scripts use `signtool.exe` from PATH or the Windows SDK. Pass `-SignTool C:\path\to\signtool.exe` when automatic discovery is not enough.

The default timestamp server is `http://timestamp.digicert.com`. Override it with `-TimestampUrl` or `CODEXBAR_SIGNING_TIMESTAMP_URL`.

## Unsigned Builds

If no certificate path is configured, signing is skipped and the portable package plus installer are still produced. This is expected for local development and contributor builds.

Use `-SkipSigning` to make the skip explicit:

```powershell
.\Scripts\package-windows-installer.ps1 -DotNet dotnet -SkipSigning
```

## GitHub Releases

Configure these repository secrets before publishing signed releases:

- `CODEXBAR_SIGNING_CERTIFICATE_BASE64`: base64-encoded PFX bytes.
- `CODEXBAR_SIGNING_CERTIFICATE_PASSWORD`: password for the PFX file.

Optional repository variable:

- `CODEXBAR_SIGNING_TIMESTAMP_URL`: timestamp server URL. Leave unset to use the script default.

Create the base64 value locally:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\to\certificate.pfx")) | Set-Clipboard
```

The Windows workflow decodes the certificate only for tag builds. If the signing secret is absent, CI prints a skip message and still publishes unsigned preview assets.

## Verification

After packaging signed assets:

```powershell
Get-AuthenticodeSignature .\dist\windows\CodexBar-Windows-0.25-win-x64\CodexBar.WinApp.exe
Get-AuthenticodeSignature .\dist\windows\CodexBar-Windows-0.25-win-x64.installer.exe
```

Expected result: `Status` is `Valid`, the signer matches the certificate subject, and the SHA256 checksum files are generated after signing.
