using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    /// <summary>
    /// Runs the official driver CLI with socket defaults that allow its connection fallback to complete.
    /// The preload applies only to this installer and its child processes; driver files and host settings are preserved.
    /// </summary>
    private static void RunPlaywrightInstaller(string[] arguments) {
        string driver = GetDriverPath();
        string preload = Path.Combine(Path.GetTempPath(), "htmltinkerx-install-" + Guid.NewGuid().ToString("N") + ".cjs");
        try {
            using (Stream source = typeof(HtmlBrowser).Assembly.GetManifestResourceStream(
                "HtmlTinkerX.Playwright.Scripts.HtmlBrowserInstallNetwork.cjs")
                ?? throw new InvalidOperationException("The browser installer network configuration is missing."))
            using (FileStream target = File.Create(preload)) {
                source.CopyTo(target);
            }

            var start = new ProcessStartInfo {
                FileName = Path.Combine(driver, "node", PlatformId, NodeExecutable),
                Arguments = string.Join(" ", new[] { "--require", preload, Path.Combine(driver, "package", "cli.js") }
                    .Concat(arguments).Select(QuoteInstallerArgument)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using Process process = Process.Start(start)
                ?? throw new InvalidOperationException("The browser installer could not be started.");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(output, errors);
            if (!string.IsNullOrWhiteSpace(output.Result)) Console.Out.Write(output.Result);
            if (!string.IsNullOrWhiteSpace(errors.Result)) Console.Error.Write(errors.Result);
            if (process.ExitCode != 0) {
                throw new InvalidOperationException($"Playwright browser installation failed with exit code {process.ExitCode}. {errors.Result.Trim()}");
            }
        } finally {
            TryDeleteFile(preload);
        }
    }

    private static string QuoteInstallerArgument(string value) {
        var result = new StringBuilder("\"");
        int slashes = 0;
        foreach (char character in value) {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
}
