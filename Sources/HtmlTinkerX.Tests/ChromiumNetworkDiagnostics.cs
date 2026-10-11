using System;
using System.IO;

namespace HtmlTinkerX.Tests;

/// <summary>Enables native Chromium socket diagnostics for selected loopback tests.</summary>
internal static class ChromiumNetworkDiagnostics {
    internal static string[] Arguments(string testName) {
        string? directory = Environment.GetEnvironmentVariable("HTMLTINKERX_TEST_NETLOG_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return Array.Empty<string>();

        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, testName + "-" + Guid.NewGuid().ToString("N") + ".json");
        return new[] { "--log-net-log=" + path };
    }
}