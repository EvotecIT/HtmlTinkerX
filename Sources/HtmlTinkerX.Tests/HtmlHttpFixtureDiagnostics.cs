using System;
using System.IO;
using System.Net;

namespace HtmlTinkerX.Tests;

/// <summary>Opt-in response completion evidence for local HTTP test fixtures.</summary>
internal static class HtmlHttpFixtureDiagnostics {
    internal static void Report(HttpListenerContext context, string stage, int? bytes = null, int? localPort = null) {
        string? debug = Environment.GetEnvironmentVariable("DEBUG");
        if (debug == null || Array.IndexOf(debug.Split(','), "pw:fixture") < 0) return;
        try {
            // Request headers, paths and bodies may contain test secrets. Correlate by
            // local port and context identity without copying those values to stderr.
            Console.Error.WriteLine($"{DateTime.UtcNow:O} pw:fixture request={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(context)} port={localPort ?? context.Request.LocalEndPoint.Port} stage={stage} bytes={bytes}");
        } catch (IOException) {
        } catch (ObjectDisposedException) {
        }
    }
}
