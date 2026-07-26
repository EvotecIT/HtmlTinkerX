using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using HtmlTinkerX;
using Microsoft.Playwright;

namespace PSParseHTML.PowerShell;

public abstract partial class AsyncPSCmdlet {
    /// <summary>Validates that proxy credentials are accompanied by a proxy address.</summary>
    protected void ValidateProxy(string? proxy, PSCredential? proxyCredential) {
        if (proxyCredential != null && string.IsNullOrWhiteSpace(proxy)) {
            ThrowTerminatingError(
                new ErrorRecord(
                    new PSArgumentException("ProxyCredential requires Proxy to be specified."),
                    "ProxyCredentialWithoutProxy",
                    ErrorCategory.InvalidArgument,
                    proxyCredential));
        }
    }

    /// <summary>Exports browser failure evidence when requested by the caller.</summary>
    protected async Task ExportFailureEvidenceIfRequestedAsync(
        HtmlBrowserSession session,
        bool enabled,
        string operation,
        Exception exception,
        string? outFolder,
        CancellationToken cancellationToken) {
        if (!enabled) {
            return;
        }

        try {
            HtmlBrowserFailureEvidenceOptions options = new() {
                Operation = operation,
                BaseFileName = operation,
                OutFolder = string.IsNullOrWhiteSpace(outFolder)
                    ? "HtmlBrowserFailureEvidence"
                    : outFolder!
            };
            HtmlBrowserEvidenceResult evidence = await HtmlBrowser.ExportFailureEvidenceAsync(
                session,
                exception,
                options,
                cancellationToken).ConfigureAwait(false);
            WriteWarning(
                $"Browser failure evidence saved to '{evidence.OutFolder}'. Manifest: '{evidence.ManifestPath}'.");
        } catch (Exception evidenceException) when (
            evidenceException is IOException or
            UnauthorizedAccessException or
            InvalidOperationException or
            PlaywrightException) {
            WriteWarning($"Browser failure evidence could not be saved: {evidenceException.Message}");
        }
    }
}
