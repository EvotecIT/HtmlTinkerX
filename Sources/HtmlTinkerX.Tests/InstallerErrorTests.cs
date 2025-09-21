using HtmlTinkerX;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class InstallerErrorTests
{
    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _prev = new();
        public EnvScope(params (string key, string? value)[] vars)
        {
            foreach (var (k, v) in vars)
            {
                _prev[k] = Environment.GetEnvironmentVariable(k);
                Environment.SetEnvironmentVariable(k, v);
            }
        }
        public void Dispose()
        {
            foreach (var kv in _prev)
                Environment.SetEnvironmentVariable(kv.Key!, kv.Value);
        }
    }

    private static Task InvokePrivateAsync(string methodName)
    {
        var m = typeof(HtmlBrowser).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method {methodName} not found");
        var task = (Task?)m.Invoke(null, null) ?? throw new InvalidOperationException("No task returned");
        return task;
    }

    private static T InvokePrivate<T>(string methodName, params object[] args)
    {
        var m = typeof(HtmlBrowser).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method {methodName} not found");
        return (T)m.Invoke(null, args)!;
    }

    private static IDisposable AcquireFileLockViaReflection()
    {
        var m = typeof(HtmlBrowser).GetMethod("AcquireInstallFileLock", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("AcquireInstallFileLock not found");
        return (IDisposable)m.Invoke(null, null)!;
    }

    private static (int Port, Task ServerTask) StartMinimalHttpServer(byte[] payload)
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        var t = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var ns = client.GetStream();
            // Read request (basic)
            var buffer = new byte[2048];
            await ns.ReadAsync(buffer, 0, buffer.Length);
            // Respond with bogus zip bytes
            string headers = $"HTTP/1.1 200 OK\r\nContent-Type: application/zip\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(headers);
            await ns.WriteAsync(headerBytes, 0, headerBytes.Length);
            await ns.WriteAsync(payload, 0, payload.Length);
            await ns.FlushAsync();
            listener.Stop();
        });
        return (port, t);
    }

    [Fact]
    public async Task CorruptZip_FromLocalServer_Throws()
    {
        // Arrange: bogus payload, local server
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        var (port, server) = StartMinimalHttpServer(Encoding.ASCII.GetBytes("NOTAZIP"));
        using var env = new EnvScope(
            ("PLAYWRIGHT_DRIVER_SEARCH_PATH", tmp),
            ("HTMLINKERX_PLAYWRIGHT_HOST", $"http://127.0.0.1:{port}")
        );
        var logs = new List<string>();
        HtmlBrowser.Logger = s => logs.Add(s);

        // Act + Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await InvokePrivateAsync("DownloadAndExtractDriverAsync"));

        // Cleanup
        HtmlBrowser.Logger = null;
        try { Directory.Delete(tmp, true); } catch { }
        await server; // ensure server task completes
    }

    [Fact]
    public async Task DriverDownload_404_Throws()
    {
        // Local server that always returns 404
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        var t = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var ns = client.GetStream();
            var buffer = new byte[512];
            await ns.ReadAsync(buffer, 0, buffer.Length);
            var headers = Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await ns.WriteAsync(headers, 0, headers.Length);
            await ns.FlushAsync();
            listener.Stop();
        });
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        using var env = new EnvScope(("PLAYWRIGHT_DRIVER_SEARCH_PATH", tmp), ("HTMLINKERX_PLAYWRIGHT_HOST", $"http://127.0.0.1:{port}"));
        await Assert.ThrowsAnyAsync<Exception>(async () => await InvokePrivateAsync("DownloadAndExtractDriverAsync"));
        try { Directory.Delete(tmp, true); } catch { }
        await t;
    }

    [Fact]
    public async Task EnsureInstalledAsync_UsesInProcessSemaphore()
    {
        // Hold the file lock to simulate another installer in progress
        var driverRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(driverRoot);
        using var env = new EnvScope(("PLAYWRIGHT_DRIVER_SEARCH_PATH", driverRoot), ("PLAYWRIGHT_BROWSERS_PATH", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))), ("HTMLINKERX_SKIP_SMOKE", "1"));
        using var held = AcquireFileLockViaReflection();

        // Stub installer to be a fast no-op
        var before = HtmlBrowser.PlaywrightInstaller;
        HtmlBrowser.PlaywrightInstaller = _ => 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var task = Task.Run(() => HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium));
        await Task.Delay(200);
        Assert.False(task.IsCompleted, "EnsureInstalledAsync should wait for file lock");
        held.Dispose();
        await task; // should complete quickly once lock released
        HtmlBrowser.PlaywrightInstaller = before;
        try { Directory.Delete(driverRoot, true); } catch { }
    }

    [Fact]
    public async Task AcquireFileLock_SerializesAcrossCalls()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        using var env = new EnvScope(("PLAYWRIGHT_DRIVER_SEARCH_PATH", tmp));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan t1Acquire = TimeSpan.Zero, t2Acquire = TimeSpan.Zero;

        var t1 = Task.Run(() =>
        {
            using var lk = AcquireFileLockViaReflection();
            t1Acquire = sw.Elapsed;
            Thread.Sleep(500);
        });
        await Task.Delay(50);
        var t2 = Task.Run(() =>
        {
            using var lk = AcquireFileLockViaReflection();
            t2Acquire = sw.Elapsed;
        });

        await Task.WhenAll(t1, t2);
        Assert.True(t2Acquire - t1Acquire >= TimeSpan.FromMilliseconds(150), $"Expected serialization: t1={t1Acquire.TotalMilliseconds} t2={t2Acquire.TotalMilliseconds}");
        try { Directory.Delete(tmp, true); } catch { }
    }

    [Fact]
    public void HermeticPath_WhenEnvZero_PointsToCoreLocalBrowsers()
    {
        var driverRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var driverPath = Path.Combine(driverRoot, ".playwright");
        var core = Path.Combine(driverPath, "package", "node_modules", "playwright-core", ".local-browsers");
        Directory.CreateDirectory(core);
        using var env = new EnvScope(("PLAYWRIGHT_DRIVER_SEARCH_PATH", driverRoot), ("PLAYWRIGHT_BROWSERS_PATH", "0"));

        string path = InvokePrivate<string>("GetBrowserInstallPath");
        Assert.Equal(Path.GetFullPath(core), Path.GetFullPath(path));
        try { Directory.Delete(driverRoot, true); } catch { }
    }

    [Fact]
    public void ZipSlip_Prevented()
    {
        using var mem = new MemoryStream();
        using (var archive = new ZipArchive(mem, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("../evil.txt");
            using var s = new StreamWriter(entry.Open());
            s.Write("x");
        }
        mem.Position = 0;
        var dest = Path.Combine(Path.GetTempPath(), "zipslip_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dest);
        using var zip = new ZipArchive(mem, ZipArchiveMode.Read);

        var m = typeof(HtmlBrowser).GetMethod("ExtractZipSafely", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ExtractZipSafely not found");
        Assert.Throws<TargetInvocationException>(() => m.Invoke(null, new object[] { zip, dest }));
        try { Directory.Delete(dest, true); } catch { }
    }

    [Fact]
    public async Task PlaywrightInstallerFailure_IsLogged()
    {
        var logs = new List<string>();
        HtmlBrowser.Logger = s => logs.Add(s);
        var before = HtmlBrowser.PlaywrightInstaller;
        var tmpBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using var env = new EnvScope(("PLAYWRIGHT_BROWSERS_PATH", tmpBrowsers), ("HTMLINKERX_SKIP_SMOKE", "1"));
        Directory.CreateDirectory(tmpBrowsers);
        try
        {
            HtmlBrowser.PlaywrightInstaller = _ => throw new Exception("install failed");
            await Assert.ThrowsAnyAsync<Exception>(async () => await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium));
            Assert.Contains(logs, s => s.Contains("Playwright install failed"));
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = before;
            HtmlBrowser.Logger = null;
            try { Directory.Delete(tmpBrowsers, true); } catch { }
        }
    }
}
