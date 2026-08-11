using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlBrowserPdfRendererContractTests {
    [Fact]
    public void ChromiumCookieExpirationRetainsUnixSecondPrecision() {
        const long expiration = 4102444801;
        HtmlBrowserPdfCookie cookie = new("session", "value", url: "https://example.test", expires: expiration);

        System.Collections.Generic.Dictionary<string, object> value = HtmlBrowserPdfRenderer.CreateCdpCookie(cookie);

        Assert.IsType<double>(value["expires"]);
        Assert.Equal((double)expiration, value["expires"]);
    }

    [Theory]
    [InlineData("ws://user:secret@example.test:8080/private?token=hidden#fragment", "ws://example.test:8080/private")]
    [InlineData("wss://user:secret@example.test/private?token=hidden#fragment", "wss://example.test/private")]
    public void BlockedWebSocketDiagnosticsRetainAuthorityAndPathWithoutSecrets(string value, string expected) {
        string sanitized = HtmlBrowserPdfRenderer.SanitizeUri(value);

        Assert.Equal(expected, sanitized);
    }

    [Fact]
    public void RendererOptionsRejectNonChromiumBeforeLaunch() {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => new HtmlBrowserPdfRendererOptions(browser: HtmlBrowserEngine.Firefox));

        Assert.Contains("only by Chromium", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RendererOwnsAFinitePreNavigationSetupDeadline() {
        HtmlBrowserPdfRendererOptions options = new();

        Assert.Equal(TimeSpan.FromSeconds(30), options.SetupTimeout);
        Assert.Throws<ArgumentOutOfRangeException>(() => new HtmlBrowserPdfRendererOptions(
            setupTimeout: TimeSpan.Zero));
    }

    [Theory]
    [InlineData(0x00006969L)]
    [InlineData(0xFF534D42L)]
    [InlineData(0xFE534D42L)]
    [InlineData(0x01021997L)]
    [InlineData(0x65735546L)]
    [InlineData(0x0BD00BD0L)]
    [InlineData(0x19830326L)]
    public void UnixPathBoundaryRecognizesRemoteAndUserSpaceFileSystems(long fileSystemType) {
        Assert.True(HtmlBrowserUnixFileSystemPath.IsRemoteFileSystemType(fileSystemType));
        Assert.False(HtmlBrowserUnixFileSystemPath.IsRemoteFileSystemType(0xEF53));
    }

    [Fact]
    public void DirectPagePdfApiUsesTheOptionsContractOnly() {
        System.Reflection.MethodInfo[] methods = typeof(HtmlBrowser).GetMethods()
            .Where(method => method.Name == nameof(HtmlBrowser.GetPagePdfAsync) || method.Name == nameof(HtmlBrowser.SavePagePdfAsync))
            .ToArray();

        Assert.Equal(2, methods.Length);
        Assert.All(methods, method => {
            System.Reflection.ParameterInfo[] parameters = method.GetParameters();
            Assert.Equal(typeof(IPage), parameters[0].ParameterType);
            Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(HtmlBrowserPdfOptions));
            Assert.True(parameters.Length <= 5);
        });
    }

    [Fact]
    public void PdfReadinessRejectsNegativeDelay() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HtmlBrowserPdfReadiness(delayMilliseconds: -1));
    }

    [Fact]
    public void CustomPageDimensionsOverrideTheDefaultNamedFormat() {
        HtmlBrowserPdfOptions options = new(width: "210mm", height: "297mm");

        Assert.Null(options.Format);
        Assert.Null(HtmlBrowserPdfCapture.CreatePageOptions(options).Format);
    }

    [Fact]
    public void DomainCookieDefaultsToTheRootPath() {
        HtmlBrowserPdfCookie cookie = new("session", "value", domain: "example.com");

        Assert.Equal("/", cookie.Path);
    }

    [Fact]
    public void RequestSnapshotsMutableCollections() {
        Dictionary<string, string> headers = new() { ["X-Correlation-Id"] = "first" };
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml("<h1>Snapshot</h1>", new Uri("https://reports.example/invoice")),
            headers: headers);

        headers["X-Correlation-Id"] = "changed";

        Assert.Equal("first", request.Headers["X-Correlation-Id"]);
    }

    [Fact]
    public void WebStorageSnapshotsPreserveCaseSensitiveKeys() {
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml("<p>storage</p>", new Uri("https://example.com/report")),
            localStorage: new Dictionary<string, string> { ["token"] = "lower", ["Token"] = "upper" });

        Assert.Equal(2, request.LocalStorage.Count);
        Assert.Equal("lower", request.LocalStorage["token"]);
        Assert.Equal("upper", request.LocalStorage["Token"]);
    }

    [Fact]
    public void IdnSourceOriginUsesTheBrowserCanonicalHost() {
        HtmlBrowserPdfSource source = HtmlBrowserPdfSource.FromUrl("https://bücher.example/report");

        Assert.Equal("xn--bcher-kva.example", source.SecurityOrigin!.IdnHost);
    }

    [Fact]
    public void RequestRejectsCredentialsWithoutAnHttpOrigin() {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<h1>No origin</h1>"),
            localStorage: new Dictionary<string, string> { ["token"] = "secret" }));

        Assert.Contains("HTTP/HTTPS base URI", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestKeepsNavigationAndReadinessTimeoutsIndependent() {
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml("<p>timeouts</p>"),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, selector: "p", timeout: 50),
            navigationTimeout: 2000,
            beforeCaptureScriptTimeout: 75,
            pdfTimeout: 125);

        Assert.Equal(50, request.Readiness.Timeout);
        Assert.Equal(2000, request.NavigationTimeout);
        Assert.Equal(75, request.BeforeCaptureScriptTimeout);
        Assert.Equal(125, request.PdfTimeout);
        Assert.Throws<ArgumentOutOfRangeException>(() => new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<p>invalid PDF timeout</p>"),
            pdfTimeout: -1));
    }

    [Fact]
    public void RequestBoundsPdfPayloadBeforeResultRetention() {
        HtmlBrowserPdfRequest request = new(HtmlBrowserPdfSource.FromHtml("<p>bounded</p>"));
        byte[] bytes = new byte[4];

        Assert.Equal(HtmlBrowserPdfRequest.DefaultMaximumPdfBytes, request.MaximumPdfBytes);
        Assert.Same(bytes, HtmlBrowserPdfCapture.ValidateOutputSize(bytes, 4));
        Assert.Same(bytes, HtmlBrowserPdfCapture.ValidateOutputSize(bytes, 0));
        Assert.Throws<InvalidOperationException>(() => HtmlBrowserPdfCapture.ValidateOutputSize(bytes, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<p>invalid limit</p>"),
            maximumPdfBytes: -1));
    }

    [Fact]
    public void BoundedCdpPrintMapsCssLengthsAndPrintFlags() {
        HtmlBrowserPdfOptions options = new(
            landscape: true,
            width: "210mm",
            height: "29.7cm",
            marginTop: "96px",
            outline: true,
            tagged: true);

        Dictionary<string, object> parameters = HtmlBrowserPdfCapture.CreateCdpPrintParameters(options);

        Assert.Equal(8.2677165354, Assert.IsType<double>(parameters["paperWidth"]), 8);
        Assert.Equal(11.6929133858, Assert.IsType<double>(parameters["paperHeight"]), 8);
        Assert.Equal(1d, Assert.IsType<double>(parameters["marginTop"]));
        Assert.True(Assert.IsType<bool>(parameters["landscape"]));
        Assert.True(Assert.IsType<bool>(parameters["generateDocumentOutline"]));
        Assert.True(Assert.IsType<bool>(parameters["generateTaggedPDF"]));
    }

    [Fact]
    public void HttpsErrorOptInAlsoConfiguresTheDedicatedChromiumProcess() {
        HtmlBrowserLaunchOptions strict = new HtmlBrowserPdfRendererOptions().CreateLaunchOptions();
        HtmlBrowserLaunchOptions trusted = new HtmlBrowserPdfRendererOptions(ignoreHttpsErrors: true).CreateLaunchOptions();

        Assert.DoesNotContain("--ignore-certificate-errors", strict.BrowserArguments);
        Assert.Contains("--ignore-certificate-errors", trusted.BrowserArguments);
    }

    [Fact]
    public void RendererOptionsRejectProxyCredentialsWithoutAProxyServer() {
        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRendererOptions(proxyUsername: "user"));
        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRendererOptions(proxyPassword: "secret"));
    }

    [Fact]
    public void RendererOptionsNormalizeBlankOptionalLaunchSettings() {
        HtmlBrowserPdfRendererOptions options = new(
            browserChannel: " ",
            browserExecutablePath: "\t",
            proxy: "  ",
            storageStatePath: " ",
            userAgent: "\t",
            locale: " ",
            timezone: "\r\n",
            networkPolicy: new HtmlBrowserNetworkPolicy(allowPrivateNetworks: true));

        Assert.Null(options.BrowserChannel);
        Assert.Null(options.BrowserExecutablePath);
        Assert.Null(options.Proxy);
        Assert.Null(options.StorageStatePath);
        Assert.Null(options.UserAgent);
        Assert.Null(options.Locale);
        Assert.Null(options.Timezone);
    }

    [Fact]
    public void ManagedPolicyProxyDisablesTrafficThatCanBypassHttpConnect() {
        HtmlBrowserLaunchOptions protectedLaunch = new HtmlBrowserPdfRendererOptions().CreateLaunchOptions();
        HtmlBrowserLaunchOptions unrestrictedLaunch = new HtmlBrowserPdfRendererOptions(
            networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()).CreateLaunchOptions();

        Assert.Contains("--force-webrtc-ip-handling-policy=disable_non_proxied_udp", protectedLaunch.BrowserArguments);
        Assert.Contains("--disable-quic", protectedLaunch.BrowserArguments);
        Assert.DoesNotContain("--force-webrtc-ip-handling-policy=disable_non_proxied_udp", unrestrictedLaunch.BrowserArguments);
        Assert.DoesNotContain("--disable-quic", unrestrictedLaunch.BrowserArguments);
        Assert.DoesNotContain("--proxy-bypass-list=<-loopback>", unrestrictedLaunch.BrowserArguments);
    }

    [Fact]
    public void BrowserTestConvenienceMethodsExposeTheHttpsOptIn() {
        string[] names = {
            nameof(HtmlBrowserTester.TestCssResourceAsync),
            nameof(HtmlBrowserTester.TestConsoleErrorsAsync),
            nameof(HtmlBrowserTester.TestPerformanceAsync)
        };

        foreach (string name in names) {
            System.Reflection.MethodInfo method = Assert.Single(typeof(HtmlBrowserTester).GetMethods(), candidate => candidate.Name == name);
            System.Reflection.ParameterInfo parameter = Assert.Single(method.GetParameters(), candidate => candidate.Name == "ignoreHttpsErrors");
            Assert.Equal(typeof(bool), parameter.ParameterType);
            Assert.Equal(false, parameter.DefaultValue);
        }
    }

    [Fact]
    public async Task PublicNetworkPolicyBlocksPrivateTargetsUnlessExplicitlyAllowed() {
        HtmlBrowserNetworkPolicyEvaluator publicOnly = new(HtmlBrowserNetworkPolicy.PublicNetworkOnly);
        HtmlBrowserNetworkPolicyEvaluator allowListed = new(new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" }));

        Assert.False(await publicOnly.IsAllowedAsync("http://127.0.0.1/report", null, CancellationToken.None));
        Assert.True(await allowListed.IsAllowedAsync("http://127.0.0.1/report", null, CancellationToken.None));
    }

    [Fact]
    public async Task SelectedFileDirectoryAllowsSiblingResourcesButNotTraversal() {
        string root = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-PdfPolicy-" + Guid.NewGuid().ToString("N"));
        string sibling = Path.Combine(root, "assets", "style.css");
        string outside = root + "-outside.css";
        Directory.CreateDirectory(Path.GetDirectoryName(sibling)!);
        File.WriteAllText(sibling, "body{}");
        File.WriteAllText(outside, "body{}");
        try {
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(HtmlBrowserNetworkPolicy.PublicNetworkOnly);

            Assert.True(await evaluator.IsAllowedAsync(new Uri(sibling).AbsoluteUri, root, CancellationToken.None));
            Assert.False(await evaluator.IsAllowedAsync(new Uri(outside).AbsoluteUri, root, CancellationToken.None));
        } finally {
            Directory.Delete(root, recursive: true);
            File.Delete(outside);
        }
    }

    [Theory]
    [InlineData(@"\\server\share\asset.css")]
    [InlineData("//server/share/asset.css")]
    [InlineData(@"\\?\UNC\server\share\asset.css")]
    [InlineData(@"\\.\PhysicalDrive0")]
    [InlineData(@"\??\C:\asset.css")]
    [InlineData(@"\Device\HarddiskVolume1\asset.css")]
    [InlineData("file://server/share/asset.css")]
    public void FilePathResolutionRejectsNetworkAndDevicePathsBeforeNormalization(string path) {
        Assert.True(HtmlBrowserFileSystemPath.IsNetworkOrDevicePath(path));
        Assert.False(HtmlBrowserFileSystemPath.TryResolveExistingPath(path, out _));
    }

    [Theory]
    [InlineData(4, null, true)]
    [InlineData(1, @"\Device\Mup\server\share", true)]
    [InlineData(3, @"\??\C:\local-substitution", true)]
    [InlineData(3, @"\Device\HarddiskVolume3", false)]
    public void WindowsDriveClassificationUsesLocalMappingsBeforePathProbes(uint driveType, string? deviceTarget, bool expected) {
        bool classified = HtmlBrowserFileSystemPath.IsWindowsUnsafeDriveRoot(
            @"Z:\",
            _ => driveType,
            _ => deviceTarget);

        Assert.Equal(expected, classified);
    }

    [Fact]
    public void WindowsReparseClassificationStopsBeforeTheTargetPathIsProbed() {
        string[] components = { @"C:\root", @"C:\root\link", @"C:\root\link\asset.css" };
        int probes = 0;

        bool classified = HtmlBrowserFileSystemPath.ContainsReparsePointBeforeTargetProbe(
            components,
            _ => ++probes == 2 ? FileAttributes.ReparsePoint : FileAttributes.Directory);

        Assert.True(classified);
        Assert.Equal(2, probes);
    }

    [Fact]
    public async Task BrowserTesterRejectsNetworkFilesBeforeExistenceChecks() {
        await Assert.ThrowsAsync<ArgumentException>(() => HtmlBrowserTester.TestFileAsync(@"\\server\share\report.html"));
    }

#if !NETFRAMEWORK
    [Fact]
    public void WindowsSubstitutedDriveIsRejectedFromLiveDosDeviceMetadata() {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
        string root = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-Subst-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "asset.css"), "body{}");
        string? drive = Enumerable.Range('D', 'Z' - 'D' + 1)
            .Select(value => ((char)value) + ":")
            .FirstOrDefault(candidate => !Directory.Exists(candidate + Path.DirectorySeparatorChar));
        Assert.False(string.IsNullOrWhiteSpace(drive));
        try {
            Assert.Equal(0, RunSubst($"{drive} \"{root}\""));
            string mappedFile = drive + Path.DirectorySeparatorChar + "asset.css";
            Assert.True(File.Exists(mappedFile));
            Assert.False(HtmlBrowserFileSystemPath.IsSafeLocalPath(mappedFile));
            Assert.False(HtmlBrowserFileSystemPath.TryResolveExistingPath(mappedFile, out _));
            Assert.Throws<ArgumentException>(() => HtmlBrowser.CreateLocalFileUri(mappedFile));
        } finally {
            if (!string.IsNullOrWhiteSpace(drive)) RunSubst($"{drive} /D");
            Directory.Delete(root, recursive: true);
        }
    }
#endif

    [Fact]
    public async Task FilePolicyRejectsRemoteFileUrisBeforePathResolution() {
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(new HtmlBrowserNetworkPolicy(
            allowFileAccess: true,
            allowedFileDirectories: new[] { Path.GetPathRoot(Path.GetFullPath("."))! }));

        Assert.False(await evaluator.IsAllowedAsync("file://server/share/asset.css", null, CancellationToken.None));
    }

#if !NETFRAMEWORK
    [Fact]
    public async Task SelectedFileDirectoryRejectsSymlinkEscape() {
        string root = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-PdfPolicy-" + Guid.NewGuid().ToString("N"));
        string outside = root + "-outside";
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        string secret = Path.Combine(outside, "secret.css");
        File.WriteAllText(secret, "body{background:red}");
        string link = Path.Combine(root, "linked-assets");
        try {
            Directory.CreateSymbolicLink(link, outside);
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(HtmlBrowserNetworkPolicy.PublicNetworkOnly);

            Assert.False(HtmlBrowserFileSystemPath.IsSafeLocalPath(Path.Combine(link, "secret.css")));
            Assert.False(await evaluator.IsAllowedAsync(new Uri(Path.Combine(link, "secret.css")).AbsoluteUri, root, CancellationToken.None));
        } finally {
            if (Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }
#endif

#if !NETFRAMEWORK
    [Fact]
    public void UnixFilePolicyRejectsSymlinksEvenWithinTheSelectedTree() {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT) return;
        string root = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-DirectPath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string target = Path.Combine(root, "target.html");
        string link = Path.Combine(root, "link.html");
        File.WriteAllText(target, "<p>safe</p>");
        try {
            File.CreateSymbolicLink(link, target);

            Assert.False(HtmlBrowserFileSystemPath.IsSafeLocalPath(link));
            Assert.False(HtmlBrowserFileSystemPath.TryResolveExistingPath(link, out _));
        } finally {
            File.Delete(link);
            File.Delete(target);
            Directory.Delete(root);
        }
    }

    [Fact]
    public void LinuxStatFallbackClassifiesAnOrdinaryFile() {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)) return;
        string path = Path.GetTempFileName();
        try {
            Assert.True(HtmlBrowserUnixFileSystemPath.TryGetLinuxStatMode(path, out uint mode));
            Assert.True(HtmlBrowserUnixFileSystemPath.IsRegularFileOrDirectoryMode(mode));
        } finally {
            File.Delete(path);
        }
    }
#endif

#if !NETFRAMEWORK
    private static int RunSubst(string arguments) {
        using Process process = Process.Start(new ProcessStartInfo("subst.exe", arguments) {
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        process.WaitForExit();
        return process.ExitCode;
    }
#endif

    [Fact]
    public async Task PublicNetworkPolicyRejectsMixedPublicAndPrivateDnsAnswers() {
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Loopback }));

        Assert.False(await evaluator.IsAllowedAsync("https://mixed.example/report", null, CancellationToken.None));
    }

    [Fact]
    public async Task UnicodeDeniedHostIsCanonicalizedLikeTheRequestUri() {
        HtmlBrowserNetworkPolicy policy = new(deniedHosts: new[] { "bücher.example" });
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            policy,
            _ => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));

        Assert.Equal("xn--bcher-kva.example", Assert.Single(policy.DeniedHosts));
        Assert.False(await evaluator.IsAllowedAsync("https://bücher.example/report", null, CancellationToken.None));
    }

    [Fact]
    public async Task FailedDnsLookupIsEvictedSoTheWarmPolicyCanRecover() {
        int calls = 0;
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Interlocked.Increment(ref calls) == 1
                ? Task.FromException<IPAddress[]>(new SocketException())
                : Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));

        Assert.False(await evaluator.IsAllowedAsync("https://recover.example/report", null, CancellationToken.None));
        Assert.True(await evaluator.IsAllowedAsync("https://recover.example/report", null, CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task SuccessfulDnsLookupExpiresInWarmPolicyEvaluator() {
        int calls = 0;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Task.FromResult(new[] { IPAddress.Parse(Interlocked.Increment(ref calls) == 1 ? "8.8.8.8" : "1.1.1.1") }),
            TimeSpan.FromSeconds(30),
            () => now);

        Assert.True(await evaluator.IsAllowedAsync("https://refresh.example/report", null, CancellationToken.None));
        Assert.True(await evaluator.IsAllowedAsync("https://refresh.example/report", null, CancellationToken.None));
        now = now.AddSeconds(31);
        Assert.True(await evaluator.IsAllowedAsync("https://refresh.example/report", null, CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CompletedDnsEntriesAreEvictedAtTheRendererCacheBound() {
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }),
            maximumDnsCacheEntries: 2);

        Assert.True(await evaluator.IsAllowedAsync("https://one.example/report", null, CancellationToken.None));
        Assert.True(await evaluator.IsAllowedAsync("https://two.example/report", null, CancellationToken.None));
        Assert.True(await evaluator.IsAllowedAsync("https://three.example/report", null, CancellationToken.None));

        Assert.Equal(2, evaluator.DnsCacheEntryCount);
    }

    [Fact]
    public async Task DnsCacheBoundNeverEvictsSharedInFlightLookups() {
        TaskCompletionSource<IPAddress[]> first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<IPAddress[]> second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Interlocked.Increment(ref calls) == 1 ? first.Task : second.Task,
            dnsLookupTimeout: TimeSpan.FromMilliseconds(25),
            maximumDnsCacheEntries: 2);

        Assert.False(await evaluator.IsAllowedAsync("https://one.example/report", null, CancellationToken.None));
        Assert.False(await evaluator.IsAllowedAsync("https://two.example/report", null, CancellationToken.None));
        Assert.False(await evaluator.IsAllowedAsync("https://three.example/report", null, CancellationToken.None));

        Assert.Equal(2, evaluator.DnsCacheEntryCount);
        Assert.Equal(2, calls);
        first.TrySetResult(new[] { IPAddress.Parse("8.8.8.8") });
        second.TrySetResult(new[] { IPAddress.Parse("8.8.8.8") });
    }

    [Fact]
    public async Task DnsLookupHasAnInternalDeadlineWithoutCallerCancellation() {
        TaskCompletionSource<IPAddress[]> pendingLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => {
                Interlocked.Increment(ref calls);
                return pendingLookup.Task;
            },
            dnsLookupTimeout: TimeSpan.FromMilliseconds(50));

        Task<bool> allowed = evaluator.IsAllowedAsync("https://timeout.example/report", null, CancellationToken.None);

        Assert.Same(allowed, await Task.WhenAny(allowed, Task.Delay(TimeSpan.FromSeconds(2))));
        Assert.False(await allowed);
        Assert.False(await evaluator.IsAllowedAsync("https://timeout.example/report", null, CancellationToken.None));
        Assert.Equal(1, calls);
        pendingLookup.TrySetResult(new[] { IPAddress.Parse("8.8.8.8") });
        Assert.True(await evaluator.IsAllowedAsync("https://timeout.example/report", null, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RejectedInitialSourceDoesNotLaunchOrChargeABrowserSlot() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => renderer.CaptureAsync(
            new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromUrl("http://127.0.0.1/rejected"))));

        HtmlBrowserPdfRendererMetrics metrics = renderer.GetMetricsSnapshot();
        Assert.Equal(0, metrics.BrowsersCreated);
        Assert.Equal(0, metrics.BrowsersRecycled);
        Assert.Equal(1, metrics.FailedCaptures);
    }

    [Fact]
    public async Task TimedOutDnsLookupsAreGloballyBoundedWithoutCachingGateSaturation() {
        TaskCompletionSource<IPAddress[]> pendingLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        ConcurrentDictionary<string, byte> startedHosts = new(StringComparer.OrdinalIgnoreCase);
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            host => {
                Interlocked.Increment(ref calls);
                startedHosts.TryAdd(host, 0);
                return pendingLookup.Task;
            },
            dnsLookupTimeout: TimeSpan.FromMilliseconds(50));
        Task<bool>[] lookups = Enumerable.Range(0, 64)
            .Select(index => evaluator.IsAllowedAsync($"https://bounded-{index}.example/report", null, CancellationToken.None))
            .ToArray();

        bool[] results = await Task.WhenAll(lookups);

        Assert.All(results, Assert.False);
        Assert.InRange(Volatile.Read(ref calls), 1, 32);
        string saturatedHost = Enumerable.Range(0, 64)
            .Select(index => $"bounded-{index}.example")
            .First(host => !startedHosts.ContainsKey(host));
        pendingLookup.TrySetResult(new[] { IPAddress.Parse("8.8.8.8") });

        bool recovered = false;
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (!recovered && DateTime.UtcNow < deadline) {
            recovered = await evaluator.IsAllowedAsync($"https://{saturatedHost}/report", null, CancellationToken.None);
            if (!recovered) await Task.Delay(10);
        }
        Assert.True(recovered);
    }

    [Theory]
    [InlineData("192.0.2.1")]
    [InlineData("192.88.99.1")]
    [InlineData("192.88.99.2")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("2001:db8::1")]
    [InlineData("64:ff9b:1::1")]
    [InlineData("100::1")]
    [InlineData("100:0:0:1::1")]
    [InlineData("2001::1")]
    [InlineData("2001:1::4")]
    [InlineData("2001:2::1")]
    [InlineData("2001:10::1")]
    [InlineData("2001:100::1")]
    [InlineData("3fff::1")]
    [InlineData("3fff:fff::1")]
    [InlineData("4000::1")]
    [InlineData("5f00::1")]
    [InlineData("64:ff9b::1")]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("64:ff9b::c000:201")]
    [InlineData("64:ff9b::c058:6301")]
    [InlineData("64:ff9b::c058:6302")]
    public async Task PublicNetworkPolicyRejectsNonGloballyReachableSpecialAddresses(string address) {
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Task.FromResult(new[] { IPAddress.Parse(address) }));

        Assert.False(await evaluator.IsAllowedAsync("https://reserved.example/report", null, CancellationToken.None));
    }

    [Theory]
    [InlineData("64:ff9b::808:808")]
    [InlineData("192.0.0.9")]
    [InlineData("192.0.0.10")]
    [InlineData("2001:1::1")]
    [InlineData("2001:1::2")]
    [InlineData("2001:1::3")]
    [InlineData("2001:3::1")]
    [InlineData("2001:4:112::1")]
    [InlineData("2001:20::1")]
    [InlineData("2001:30::1")]
    [InlineData("3fff:1000::1")]
    public async Task PublicNetworkPolicyAllowsGloballyReachableSpecialAssignments(string address) {
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Task.FromResult(new[] { IPAddress.Parse(address) }));

        Assert.True(await evaluator.IsAllowedAsync("https://public.example/report", null, CancellationToken.None));
    }

    [Fact]
    public async Task PolicyProxyConnectsToTheExactAddressApprovedByTheEvaluator() {
        TcpListener origin = new(IPAddress.Loopback, 0);
        origin.Start();
        try {
            int originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
            Task originTask = Task.Run(async () => {
                using TcpClient accepted = await origin.AcceptTcpClientAsync();
                using NetworkStream stream = accepted.GetStream();
                byte[] request = new byte[4096];
                int read = await stream.ReadAsync(request, 0, request.Length);
                Assert.Contains("GET /bound HTTP/1.1", Encoding.ASCII.GetString(request, 0, read), StringComparison.Ordinal);
                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nbound");
                await stream.WriteAsync(response, 0, response.Length);
            });
            HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "render.invalid" });
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy, _ => Task.FromResult(new[] { IPAddress.Loopback }));
            await using HtmlBrowserPolicyProxy proxy = new(evaluator);
            Uri proxyUri = new(proxy.Server);
            using TcpClient browser = new();
            await browser.ConnectAsync(IPAddress.Loopback, proxyUri.Port);
            using NetworkStream browserStream = browser.GetStream();
            byte[] payload = Encoding.ASCII.GetBytes($"GET http://render.invalid:{originPort}/bound HTTP/1.1\r\nHost: render.invalid:{originPort}\r\n\r\n");
            await browserStream.WriteAsync(payload, 0, payload.Length);
            using MemoryStream responseBytes = new();
            await browserStream.CopyToAsync(responseBytes);

            Assert.EndsWith("bound", Encoding.ASCII.GetString(responseBytes.ToArray()), StringComparison.Ordinal);
            await originTask;
        } finally {
            origin.Stop();
        }
    }

    [Fact]
    public async Task PolicyProxyDoesNotForwardProxyAuthorizationToTheOrigin() {
        TcpListener origin = new(IPAddress.Loopback, 0);
        origin.Start();
        try {
            int originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
            Task<string> originTask = Task.Run(async () => {
                using TcpClient accepted = await origin.AcceptTcpClientAsync();
                using NetworkStream stream = accepted.GetStream();
                byte[] request = new byte[4096];
                int read = await stream.ReadAsync(request, 0, request.Length);
                string received = Encoding.ASCII.GetString(request, 0, read);
                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");
                await stream.WriteAsync(response, 0, response.Length);
                return received;
            });
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(
                new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "render.invalid" }),
                _ => Task.FromResult(new[] { IPAddress.Loopback }));
            await using HtmlBrowserPolicyProxy proxy = new(evaluator);
            Uri proxyUri = new(proxy.Server);
            using TcpClient browser = new();
            await browser.ConnectAsync(IPAddress.Loopback, proxyUri.Port);
            using NetworkStream browserStream = browser.GetStream();
            byte[] payload = Encoding.ASCII.GetBytes($"GET http://render.invalid:{originPort}/ HTTP/1.1\r\nHost: render.invalid:{originPort}\r\nProxy-Authorization: Basic c2VjcmV0\r\n\r\n");
            await browserStream.WriteAsync(payload, 0, payload.Length);
            using MemoryStream responseBytes = new();
            await browserStream.CopyToAsync(responseBytes);

            string originRequest = await originTask;
            Assert.DoesNotContain("Proxy-Authorization:", originRequest, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("ok", Encoding.ASCII.GetString(responseBytes.ToArray()), StringComparison.Ordinal);
        } finally {
            origin.Stop();
        }
    }

    [Fact]
    public async Task PolicyProxyPreservesTunnelBytesCoalescedWithConnectHeaders() {
        TcpListener origin = new(IPAddress.Loopback, 0);
        origin.Start();
        try {
            int originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
            byte[] tunnelPayload = Encoding.ASCII.GetBytes("coalesced-client-hello");
            Task originTask = Task.Run(async () => {
                using TcpClient accepted = await origin.AcceptTcpClientAsync();
                using NetworkStream stream = accepted.GetStream();
                byte[] received = new byte[tunnelPayload.Length];
                int offset = 0;
                while (offset < received.Length) {
                    int read = await stream.ReadAsync(received, offset, received.Length - offset);
                    if (read == 0) break;
                    offset += read;
                }
                Assert.Equal(tunnelPayload, received);
                byte[] response = Encoding.ASCII.GetBytes("tunnel-response");
                await stream.WriteAsync(response, 0, response.Length);
            });
            HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "render.invalid" });
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy, _ => Task.FromResult(new[] { IPAddress.Loopback }));
            await using HtmlBrowserPolicyProxy proxy = new(evaluator);
            Uri proxyUri = new(proxy.Server);
            using TcpClient browser = new();
            await browser.ConnectAsync(IPAddress.Loopback, proxyUri.Port);
            using NetworkStream browserStream = browser.GetStream();
            byte[] connect = Encoding.ASCII.GetBytes($"CONNECT render.invalid:{originPort} HTTP/1.1\r\nHost: render.invalid:{originPort}\r\n\r\n");
            byte[] request = connect.Concat(tunnelPayload).ToArray();
            await browserStream.WriteAsync(request, 0, request.Length);
            using MemoryStream responseBytes = new();
            await browserStream.CopyToAsync(responseBytes);

            string responseText = Encoding.ASCII.GetString(responseBytes.ToArray());
            Assert.Contains("200 Connection Established", responseText, StringComparison.Ordinal);
            Assert.EndsWith("tunnel-response", responseText, StringComparison.Ordinal);
            await originTask;
        } finally {
            origin.Stop();
        }
    }

    [Fact]
    public async Task PolicyProxyBoundsAReversePumpAfterTheBrowserHalfCloses() {
        TcpListener origin = new(IPAddress.Loopback, 0);
        origin.Start();
        using CancellationTokenSource originLifetime = new();
        Task originTask = Task.CompletedTask;
        try {
            int originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
            originTask = Task.Run(async () => {
                try {
                    using TcpClient accepted = await origin.AcceptTcpClientAsync();
                    using NetworkStream stream = accepted.GetStream();
                    byte[] payload = new byte[1];
                    Assert.Equal(1, await stream.ReadAsync(payload, 0, payload.Length));
                    try { await Task.Delay(Timeout.Infinite, originLifetime.Token); } catch (OperationCanceledException) { }
                } catch (SocketException) when (originLifetime.IsCancellationRequested) {
                } catch (ObjectDisposedException) when (originLifetime.IsCancellationRequested) {
                }
            });
            HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "render.invalid" });
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy, _ => Task.FromResult(new[] { IPAddress.Loopback }));
            await using HtmlBrowserPolicyProxy proxy = new(evaluator, relayDrainTimeout: TimeSpan.FromMilliseconds(50));
            Uri proxyUri = new(proxy.Server);
            using TcpClient browser = new();
            await browser.ConnectAsync(IPAddress.Loopback, proxyUri.Port);
            using NetworkStream browserStream = browser.GetStream();
            byte[] connect = Encoding.ASCII.GetBytes($"CONNECT render.invalid:{originPort} HTTP/1.1\r\nHost: render.invalid:{originPort}\r\n\r\nx");
            await browserStream.WriteAsync(connect, 0, connect.Length);
            browser.Client.Shutdown(SocketShutdown.Send);

            using MemoryStream responseBytes = new();
            Task readResponse = browserStream.CopyToAsync(responseBytes);
            Assert.Same(readResponse, await Task.WhenAny(readResponse, Task.Delay(TimeSpan.FromSeconds(2))));
            await readResponse;
            Assert.Contains("200 Connection Established", Encoding.ASCII.GetString(responseBytes.ToArray()), StringComparison.Ordinal);
        } finally {
            originLifetime.Cancel();
            origin.Stop();
            await originTask;
        }
    }

    [Fact]
    public async Task PolicyProxyBoundsOutboundConnectionAttempts() {
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "render.invalid" });
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy, _ => Task.FromResult(new[] { IPAddress.Loopback }));
        TaskCompletionSource<bool> neverConnects = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using HtmlBrowserPolicyProxy proxy = new(
            evaluator,
            TimeSpan.FromMilliseconds(50),
            (_, _, _) => neverConnects.Task);
        Uri proxyUri = new(proxy.Server);
        using TcpClient browser = new();
        await browser.ConnectAsync(IPAddress.Loopback, proxyUri.Port);
        using NetworkStream browserStream = browser.GetStream();
        byte[] payload = Encoding.ASCII.GetBytes("GET http://render.invalid:8080/bound HTTP/1.1\r\nHost: render.invalid:8080\r\n\r\n");
        await browserStream.WriteAsync(payload, 0, payload.Length);
        using MemoryStream responseBytes = new();
        await browserStream.CopyToAsync(responseBytes);

        Assert.Contains("403 Forbidden", Encoding.ASCII.GetString(responseBytes.ToArray()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PolicyProxyDisposalClosesClientsBlockedBeforeHeaders() {
        HtmlBrowserPolicyProxy proxy = new(HtmlBrowserNetworkPolicy.PublicNetworkOnly);
        using TcpClient client = new();
        Uri endpoint = new(proxy.Server);
        await client.ConnectAsync(IPAddress.Loopback, endpoint.Port);
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (proxy.ActiveClientCount == 0 && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.Equal(1, proxy.ActiveClientCount);

        Task disposal = proxy.DisposeAsync().AsTask();

        Assert.Same(disposal, await Task.WhenAny(disposal, Task.Delay(TimeSpan.FromSeconds(2))));
        await disposal;
    }

    [Fact]
    public async Task PolicyProxyFallsBackAfterAnAddressAttemptTimesOut() {
        TcpListener origin = new(IPAddress.Loopback, 0);
        origin.Start();
        try {
            int originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
            Task originTask = Task.Run(async () => {
                using TcpClient accepted = await origin.AcceptTcpClientAsync();
                using NetworkStream stream = accepted.GetStream();
                byte[] request = new byte[4096];
                int read = await stream.ReadAsync(request, 0, request.Length);
                Assert.Contains("GET /fallback HTTP/1.1", Encoding.ASCII.GetString(request, 0, read), StringComparison.Ordinal);
                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 8\r\nConnection: close\r\n\r\nfallback");
                await stream.WriteAsync(response, 0, response.Length);
            });
            IPAddress stalledAddress = IPAddress.Parse("127.0.0.2");
            HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "render.invalid" });
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(
                policy,
                _ => Task.FromResult(new[] { stalledAddress, IPAddress.Loopback }));
            TaskCompletionSource<bool> neverConnects = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await using HtmlBrowserPolicyProxy proxy = new(
                evaluator,
                TimeSpan.FromSeconds(5),
                (client, address, port) => address.Equals(stalledAddress)
                    ? neverConnects.Task
                    : client.ConnectAsync(address, port));
            Uri proxyUri = new(proxy.Server);
            using TcpClient browser = new();
            await browser.ConnectAsync(IPAddress.Loopback, proxyUri.Port);
            using NetworkStream browserStream = browser.GetStream();
            byte[] payload = Encoding.ASCII.GetBytes($"GET http://render.invalid:{originPort}/fallback HTTP/1.1\r\nHost: render.invalid:{originPort}\r\n\r\n");
            await browserStream.WriteAsync(payload, 0, payload.Length);
            using MemoryStream responseBytes = new();
            await browserStream.CopyToAsync(responseBytes);

            Assert.EndsWith("fallback", Encoding.ASCII.GetString(responseBytes.ToArray()), StringComparison.Ordinal);
            await originTask;
        } finally {
            origin.Stop();
        }
    }

    [Fact]
    public void PooledPdfContractsDoNotExposePlaywrightTypes() {
        Type[] contractTypes = {
            typeof(HtmlBrowserPdfRendererOptions), typeof(HtmlBrowserPdfRequest), typeof(HtmlBrowserPdfCookie),
            typeof(HtmlBrowserPdfOptions), typeof(HtmlBrowserPdfReadiness), typeof(HtmlBrowserPdfResult)
        };

        Assert.DoesNotContain(contractTypes.SelectMany(type => type.GetProperties()), property =>
            string.Equals(property.PropertyType.Namespace, "Microsoft.Playwright", StringComparison.Ordinal));
        Assert.Equal(HtmlBrowserCookieSameSite.Strict, new HtmlBrowserPdfCookie("session", "value", url: "https://example.com", sameSite: HtmlBrowserCookieSameSite.Strict).SameSite);
    }

    [Fact]
    public void PdfCookieRejectsMixedUrlAndDomainPathScope() {
        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfCookie(
            "session",
            "value",
            url: "https://example.com",
            domain: "example.com"));
        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfCookie(
            "session",
            "value",
            url: "https://example.com",
            path: "/reports"));
    }

    [Fact]
    public void PdfCookieNormalizesWhitespaceOnlyScopeFields() {
        HtmlBrowserPdfCookie domainCookie = new("session", "value", url: " ", domain: "example.com", path: "\t");
        HtmlBrowserPdfCookie urlCookie = new("session", "value", url: "https://example.com", domain: " ", path: "\r\n");

        Assert.Null(domainCookie.Url);
        Assert.Equal("example.com", domainCookie.Domain);
        Assert.Equal("/", domainCookie.Path);
        Assert.Equal("https://example.com", urlCookie.Url);
        Assert.Null(urlCookie.Domain);
        Assert.Null(urlCookie.Path);
    }

    [Fact]
    public void PdfFileSourceRejectsNetworkAndDevicePathsBeforeNormalization() {
        Assert.Throws<ArgumentException>(() => HtmlBrowserPdfSource.FromFile(@"\\server\share\report.html"));
        Assert.Throws<ArgumentException>(() => HtmlBrowserPdfSource.FromFile("file://server/share/report.html"));
        Assert.Throws<ArgumentException>(() => HtmlBrowserPdfSource.FromFile(@"\\?\C:\reports\report.html"));
        Assert.Throws<ArgumentException>(() => HtmlBrowserPdfSource.FromFile(@"\??\C:\reports\report.html"));
    }

    [Fact]
    public void PdfHtmlSourceRejectsNetworkFileBasesBeforeNormalization() {
        Assert.Throws<ArgumentException>(() => HtmlBrowserPdfSource.FromHtml(
            "<p>unsafe base</p>",
            new Uri("file://server/share/report.html")));
    }

    [Fact]
    public void PublicNetworkEnforcementRejectsCallerProxyWhoseDnsCannotBeBound() {
        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRenderer(new HtmlBrowserPdfRendererOptions(proxy: "http://proxy.example:8080")));
    }

    [Fact]
    public async Task TrustedCallerProxyCanResolveHostsUnavailableToTheRenderer() {
        int resolverCalls = 0;
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            new HtmlBrowserNetworkPolicy(allowPrivateNetworks: true),
            _ => {
                Interlocked.Increment(ref resolverCalls);
                throw new SocketException((int)SocketError.HostNotFound);
            });

        bool allowed = await evaluator.IsAllowedAsync(
            "http://renderer.proxy-only.invalid/report",
            selectedFileDirectory: null,
            deferNetworkResolutionToProxy: true,
            CancellationToken.None);

        Assert.True(allowed);
        Assert.Equal(0, resolverCalls);
    }

    [Fact]
    public void HostRulesRejectCallerProxyBecauseWebSocketTunnelsCannotBeEnforced() {
        HtmlBrowserNetworkPolicy policy = new(
            allowPrivateNetworks: true,
            deniedHosts: new[] { "internal.example" });

        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRenderer(new HtmlBrowserPdfRendererOptions(
            proxy: "http://proxy.example:8080",
            networkPolicy: policy)));
    }

    [Fact]
    public void BrowserSessionsAndPooledRendererValidateHttpsByDefault() {
        Assert.False(new HtmlBrowserLaunchOptions().IgnoreHTTPSErrors);
        Assert.False(new HtmlBrowserPdfRendererOptions().IgnoreHttpsErrors);
    }
}
