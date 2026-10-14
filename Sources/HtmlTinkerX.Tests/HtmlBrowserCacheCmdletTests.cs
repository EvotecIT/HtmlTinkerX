#if !NETFRAMEWORK
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.Json;
using PSParseHTML.PowerShell;
using Xunit.Abstractions;

namespace HtmlTinkerX.Tests;

[Collection("Process diagnostics")]
public class HtmlBrowserCacheCmdletTests {
    private readonly ITestOutputHelper _output;

    public HtmlBrowserCacheCmdletTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CacheCmdlet_WhatIfPreservesOwnedTempEvenWithForce(bool force) {
        using var fixture = new IsolatedCmdletTemp();
        using var runspace = CreateRunspace();
        using var command = PowerShell.Create();
        command.Runspace = runspace;
        fixture.VerifyExclusiveRoute();
        command.AddCommand("Clear-HtmlBrowserCache").AddParameter("SkipBrowsers", true)
            .AddParameter("Force", force).AddParameter("WhatIf", true);

        var output = command.Invoke();

        _output.WriteLine(JsonSerializer.Serialize(new {
            fixture.InitialTemp, ActualTemp = Path.GetTempPath(), fixture.Root,
            SkipBrowsers = true, Force = force, WhatIf = true,
            SentinelExists = File.Exists(fixture.Sentinel), CmdletAssembly = typeof(ClearHtmlBrowserCacheCommand).Assembly.Location,
            Output = output.Select(item => item.ToString()).ToArray(), Errors = command.Streams.Error.Count
        }));
        Assert.Empty(command.Streams.Error);
        Assert.True(File.Exists(fixture.Sentinel));
        Assert.Equal("owned temp bytes", File.ReadAllText(fixture.Sentinel));
    }

    [Fact]
    public void CacheCmdlet_ConfirmedCleanupRemovesOnlyOwnedTemp() {
        using var fixture = new IsolatedCmdletTemp();
        using var runspace = CreateRunspace();
        using var command = PowerShell.Create();
        command.Runspace = runspace;
        fixture.VerifyExclusiveRoute();
        command.AddCommand("Clear-HtmlBrowserCache").AddParameter("SkipBrowsers", true)
            .AddParameter("Force", true).AddParameter("Confirm", false);

        command.Invoke();

        Assert.Empty(command.Streams.Error);
        Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.Sentinel)));
        Assert.True(Directory.Exists(fixture.Root));
    }

    [Fact]
    public void CacheCmdlet_SharedEstimateCountsNestedAndDuplicateFootprintsOnce() {
        using var fixture = new IsolatedCmdletTemp();
        var child = new HtmlBrowserCacheCleaner.CacheLocation { Path = Path.GetDirectoryName(fixture.Sentinel)!, Size = 3 };
        var parent = new HtmlBrowserCacheCleaner.CacheLocation { Path = fixture.Root, Size = 8 };
        var duplicate = new HtmlBrowserCacheCleaner.CacheLocation { Path = Path.Combine(fixture.Root, "."), Size = 80 };

        Assert.Equal(8, HtmlBrowserCacheCleaner.GetEstimatedTotalSize(new[] { child, parent, duplicate }));
        Assert.Equal(80, HtmlBrowserCacheCleaner.GetEstimatedTotalSize(new[] { duplicate, child, parent }));
        Assert.Throws<ArgumentNullException>(() => HtmlBrowserCacheCleaner.GetEstimatedTotalSize(null!));
        Assert.Throws<ArgumentException>(() => HtmlBrowserCacheCleaner.GetEstimatedTotalSize(new HtmlBrowserCacheCleaner.CacheLocation[] { null! }));
        Assert.True(File.Exists(fixture.Sentinel));
        Assert.Equal(3, child.Size);
        Assert.Equal(8, parent.Size);
    }

    private static Runspace CreateRunspace() {
        var state = InitialSessionState.Create();
        state.Commands.Add(new SessionStateCmdletEntry("Clear-HtmlBrowserCache", typeof(ClearHtmlBrowserCacheCommand), null));
        var runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        return runspace;
    }

    private sealed class IsolatedCmdletTemp : IDisposable {
        private readonly Dictionary<string, string?> _environment = new();
        public string InitialTemp { get; } = Path.GetTempPath();
        public string Root { get; }
        public string Sentinel { get; }

        public IsolatedCmdletTemp() {
            Root = Path.Combine(InitialTemp, "cache-cmdlet-isolation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Assert.Empty(Directory.EnumerateFileSystemEntries(Root));
            foreach (string name in new[] { "TMPDIR", "TEMP", "TMP" }) {
                _environment[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, Root);
            }
            Sentinel = Path.Combine(Root, "playwright-owned", "preserve");
            Directory.CreateDirectory(Path.GetDirectoryName(Sentinel)!);
            File.WriteAllText(Sentinel, "owned temp bytes");
        }

        public void VerifyExclusiveRoute() {
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            Assert.True(string.Equals(Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), comparison),
                "Refusing to invoke cleanup outside the disposable cmdlet temp root.");
            var location = Assert.Single(HtmlBrowserCacheCleaner.GetCacheLocations(includeBrowsers: false));
            Assert.Equal(Path.GetDirectoryName(Sentinel), location.Path);
            Assert.Single(Directory.GetDirectories(Root));
        }

        public void Dispose() {
            foreach (var pair in _environment) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
#endif
