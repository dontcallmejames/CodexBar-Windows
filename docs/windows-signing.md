# Windows Signing

CodexBar can build unsigned local installers by default. Public Windows releases can be signed when a code-signing PFX certificate is available.

Signing reduces install friction and helps users identify the publisher. It does not immediately remove every Microsoft Defender SmartScreen warning; SmartScreen reputation builds over time for the certificate and downloaded file.

## Local Signing

Build the portable package first, then build and sign the installer:

```powershell
.\Scripts\package-windows.ps1 -DotNet dotnet
.\Scripts\package-windows-installer.ps1 `
  -DotNet dotnet `
  -SkipPortablePackage `
  -SigningCertificatePath C:\path\to\certificate.pfx `
  -SigningCertificatePassword $env:CODEXBAR_SIGNING_CERTIFICATE_PASSWORD
```

The installer script uses `signtool.exe` from PATH or the Windows SDK. Pass `-SignTool C:\path\to\signtool.exe` when automatic discovery is not enough.

The default timestamp server is `http://timestamp.digicert.com`. Override it with `-TimestampUrl` or `CODEXBAR_SIGNING_TIMESTAMP_URL`.

## Unsigned Builds

If no certificate path is configured, signing is skipped and the installer is still produced. This is expected for local development and contributor builds.

Use `-SkipSigning` to make the skip explicit:

```powershell
.\Scripts\package-windows-installer.ps1 -DotNet dotnet -SkipPortablePackage -SkipSigning
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

After packaging a signed installer:

```powershell
Get-AuthenticodeSignature .\dist\windows\CodexBar-Windows-0.25-win-x64.installer.exe
```

Expected result: `Status` is `Valid`, the signer matches the certificate subject, and the SHA256 checksum file is generated after signing.
