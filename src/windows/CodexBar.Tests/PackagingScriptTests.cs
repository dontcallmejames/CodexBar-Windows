namespace CodexBar.Tests;

[TestClass]
public sealed class PackagingScriptTests
{
    [TestMethod]
    public void WindowsPackageScriptWritesSha256Checksum()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "Scripts",
            "package-windows.ps1"));
        var script = File.ReadAllText(scriptPath);

        StringAssert.Contains(script, "Get-FileHash");
        StringAssert.Contains(script, ".sha256");
        StringAssert.Contains(script, "BUILD_NUMBER");
        StringAssert.Contains(script, "WINDOWS_PREVIEW_NUMBER");
        StringAssert.Contains(script, "-p:InformationalVersion=$version");
        StringAssert.Contains(script, "-p:IncludeSourceRevisionInInformationalVersion=false");
        StringAssert.Contains(script, "-p:BuildNumber=$buildNumber");
        StringAssert.Contains(script, "-p:WindowsPreviewNumber=$windowsPreviewNumber");
    }

    [TestMethod]
    public void WindowsPackageScriptCanSignPublishedExecutableBeforePortableZip()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "Scripts",
            "package-windows.ps1"));
        var script = File.ReadAllText(scriptPath);

        StringAssert.Contains(script, "SigningCertificatePath");
        StringAssert.Contains(script, "Invoke-WindowsCodeSigning $appExecutablePath");
        StringAssert.Contains(script, "CodexBar.WinUI.exe");

        var signIndex = script.IndexOf("Invoke-WindowsCodeSigning $appExecutablePath", StringComparison.Ordinal);
        var zipIndex = script.IndexOf("Compress-Archive", StringComparison.Ordinal);
        Assert.IsTrue(signIndex >= 0, "The published executable should be signed.");
        Assert.IsTrue(zipIndex > signIndex, "The portable zip must be created after executable signing.");
    }

    [TestMethod]
    public void WindowsWorkflowPackagesOnlyTagBuilds()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            ".github",
            "workflows",
            "windows.yml"));
        var workflow = File.ReadAllText(workflowPath);

        StringAssert.Contains(workflow, "tags:");
        StringAssert.Contains(workflow, "v*");
        StringAssert.Contains(workflow, "if: startsWith(github.ref, 'refs/tags/')");
    }

    [TestMethod]
    public void WindowsWorkflowPublishesPreviewReleaseAssetsForTags()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            ".github",
            "workflows",
            "windows.yml"));
        var workflow = File.ReadAllText(workflowPath);

        StringAssert.Contains(workflow, "contents: write");
        StringAssert.Contains(workflow, "softprops/action-gh-release");
        // Preview tags publish as prereleases, stable (vX.Y.Z) tags as full releases.
        StringAssert.Contains(workflow, "prerelease: ${{ contains(github.ref_name, '-preview.') }}");
        StringAssert.Contains(workflow, "dist/windows/*.zip");
        StringAssert.Contains(workflow, "dist/windows/*.zip.sha256");
    }

    [TestMethod]
    public void WindowsInstallerScriptBuildsInnoSetupInstallerAndChecksum()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            ".."));
        var scriptPath = Path.Combine(repoRoot, "Scripts", "package-windows-installer.ps1");
        var innoPath = Path.Combine(repoRoot, "installer", "windows", "CodexBar.iss");

        Assert.IsTrue(File.Exists(scriptPath), scriptPath);
        Assert.IsTrue(File.Exists(innoPath), innoPath);
        var script = File.ReadAllText(scriptPath);
        var inno = File.ReadAllText(innoPath);

        StringAssert.Contains(script, "ISCC.exe");
        StringAssert.Contains(script, "package-windows.ps1");
        StringAssert.Contains(script, "Get-FileHash");
        StringAssert.Contains(script, ".installer.exe.sha256");
        StringAssert.Contains(inno, "CodexBar for Windows");
        StringAssert.Contains(inno, "CodexBar.WinUI.exe");
        StringAssert.Contains(inno, "{group}\\CodexBar");
    }

    [TestMethod]
    public void WindowsInstallerScriptSupportsOptionalCodeSigning()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "Scripts",
            "package-windows-installer.ps1"));
        var script = File.ReadAllText(scriptPath);

        StringAssert.Contains(script, "SigningCertificatePath");
        StringAssert.Contains(script, "CODEXBAR_SIGNING_CERTIFICATE_PATH");
        StringAssert.Contains(script, "CODEXBAR_SIGNING_CERTIFICATE_PASSWORD");
        StringAssert.Contains(script, "TimestampUrl");
        StringAssert.Contains(script, "signtool.exe");
        StringAssert.Contains(script, "Invoke-WindowsCodeSigning");
        StringAssert.Contains(script, "Signing skipped");
    }

    [TestMethod]
    public void WindowsInstallerScriptSignsExecutableBeforeCompilingInstallerWhenSkippingPortablePackage()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "Scripts",
            "package-windows-installer.ps1"));
        var script = File.ReadAllText(scriptPath);

        StringAssert.Contains(script, "$appExecutablePath = Join-Path $publishDir \"CodexBar.WinUI.exe\"");
        StringAssert.Contains(script, "Invoke-WindowsCodeSigning $appExecutablePath");
        StringAssert.Contains(script, "Invoke-WindowsCodeSigning $installerPath");

        var executableSignIndex = script.IndexOf("Invoke-WindowsCodeSigning $appExecutablePath", StringComparison.Ordinal);
        var innoIndex = script.IndexOf("& $iscc", StringComparison.Ordinal);
        var installerSignIndex = script.IndexOf("Invoke-WindowsCodeSigning $installerPath", StringComparison.Ordinal);
        Assert.IsTrue(executableSignIndex >= 0, "The published executable should be signed.");
        Assert.IsTrue(innoIndex > executableSignIndex, "The executable must be signed before Inno Setup packages it.");
        Assert.IsTrue(installerSignIndex > innoIndex, "The installer must be signed after Inno Setup creates it.");
    }

    [TestMethod]
    public void WindowsWorkflowPublishesInstallerAssetsForTags()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            ".github",
            "workflows",
            "windows.yml"));
        var workflow = File.ReadAllText(workflowPath);

        StringAssert.Contains(workflow, "choco install innosetup");
        StringAssert.Contains(workflow, "package-windows-installer.ps1");
        StringAssert.Contains(workflow, "dist/windows/*.installer.exe");
        StringAssert.Contains(workflow, "dist/windows/*.installer.exe.sha256");
    }

    [TestMethod]
    public void WindowsWorkflowSignsPublishDirBeforeZippingAndInstallerPackaging()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            ".github",
            "workflows",
            "windows.yml"));
        var workflow = File.ReadAllText(workflowPath);

        // Required ordering: publish → sign exe → zip → installer → sign installer.
        // Signing the publish directory FIRST ensures both the portable zip and the
        // Inno installer payload embed the signed CodexBar.WinUI.exe.
        StringAssert.Contains(workflow, "-PublishOnly");
        StringAssert.Contains(workflow, "-SkipPublish -SkipSigning");
        StringAssert.Contains(workflow, "-SkipPortablePackage -SkipSigning");

        var publishIdx = workflow.IndexOf("Publish self-contained app", StringComparison.Ordinal);
        var signExeIdx = workflow.IndexOf("Sign CodexBar.WinUI.exe via Trusted Signing", StringComparison.Ordinal);
        var zipIdx = workflow.IndexOf("Build portable zip from signed publish dir", StringComparison.Ordinal);
        var installerIdx = workflow.IndexOf("Build installer from signed publish dir", StringComparison.Ordinal);
        var signInstallerIdx = workflow.IndexOf("Sign installer via Trusted Signing", StringComparison.Ordinal);

        Assert.IsTrue(publishIdx >= 0 && signExeIdx > publishIdx, "Exe signing must follow publish.");
        Assert.IsTrue(zipIdx > signExeIdx, "Portable zip must be built after exe signing.");
        Assert.IsTrue(installerIdx > signExeIdx, "Installer must be built after exe signing.");
        Assert.IsTrue(signInstallerIdx > installerIdx, "Installer signing must follow installer build.");
    }

    [TestMethod]
    public void WindowsWorkflowSignsViaAzureTrustedSigningWhenConfigured()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            ".github",
            "workflows",
            "windows.yml"));
        var workflow = File.ReadAllText(workflowPath);

        // OIDC federation to Microsoft Entra requires the id-token permission and azure/login.
        StringAssert.Contains(workflow, "id-token: write");
        StringAssert.Contains(workflow, "azure/login@v2");
        StringAssert.Contains(workflow, "azure/trusted-signing-action");

        // The six Trusted Signing variables that gate the signing steps.
        StringAssert.Contains(workflow, "TRUSTED_SIGNING_ENDPOINT");
        StringAssert.Contains(workflow, "TRUSTED_SIGNING_ACCOUNT_NAME");
        StringAssert.Contains(workflow, "TRUSTED_SIGNING_PROFILE_NAME");
        StringAssert.Contains(workflow, "AZURE_CLIENT_ID");
        StringAssert.Contains(workflow, "AZURE_TENANT_ID");
        StringAssert.Contains(workflow, "AZURE_SUBSCRIPTION_ID");

        // When signing is not configured the workflow still publishes unsigned assets.
        StringAssert.Contains(workflow, "Trusted Signing variables not set");

        // Signed binaries must have their SHA256 checksums refreshed. The portable zip
        // is hashed by package-windows.ps1 itself (since the exe is signed before zip
        // is built); the installer needs an explicit refresh because Trusted Signing
        // stamps it after Inno Setup produces it.
        StringAssert.Contains(workflow, "Refresh installer checksum");
    }
}
