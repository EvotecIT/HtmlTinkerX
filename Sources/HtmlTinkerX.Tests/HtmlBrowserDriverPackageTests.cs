using HtmlTinkerX;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public class HtmlBrowserDriverPackageTests
{
    [Fact]
    public void GetSafeExtractionPath_RejectsCaseVariantSiblingEscapeOnCaseSensitivePlatforms()
    {
        string root = Path.Combine(Path.GetTempPath(), "playwright-root");
        Assert.Throws<InvalidDataException>(() =>
            HtmlBrowser.GetSafeExtractionPath(root, "../PLAYWRIGHT-ROOT/escaped"));
    }

    [Fact]
    public async Task AcquireInstallationFileLockAsync_SerializesIndependentCallers()
    {
        using FileStream firstLock = await HtmlBrowser.AcquireInstallationFileLockAsync();
        Task<FileStream> secondLockTask = Task.Run(async () =>
            await HtmlBrowser.AcquireInstallationFileLockAsync());

        await Task.Delay(200);
        Assert.False(secondLockTask.IsCompleted);

        firstLock.Dispose();
        Task completedTask = await Task.WhenAny(secondLockTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(secondLockTask, completedTask);
        using FileStream secondLock = await secondLockTask;
        Assert.True(secondLock.CanWrite);
    }

    [Fact]
    public async Task CleanInstallationAsync_WaitsForActiveInstallerBeforeDeletingSharedRoots()
    {
        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string? originalBrowsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");

        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
            Directory.CreateDirectory(tempBrowsers);
            Directory.CreateDirectory(tempDriver);

            using FileStream installationLock = await HtmlBrowser.AcquireInstallationFileLockAsync();
            Task cleanTask = Task.Run(async () => await HtmlBrowser.CleanInstallationAsync());

            await Task.Delay(200);
            Assert.False(cleanTask.IsCompleted);
            Assert.True(Directory.Exists(tempBrowsers));
            Assert.True(Directory.Exists(tempDriver));

            installationLock.Dispose();
            Task completedTask = await Task.WhenAny(cleanTask, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(cleanTask, completedTask);
            await cleanTask;
            Assert.False(Directory.Exists(tempBrowsers));
            Assert.False(Directory.Exists(tempDriver));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", originalBrowsersPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    [Fact]
    public async Task CleanCache_WaitsForActiveInstallerBeforeDeletingSelectedLocation()
    {
        string tempCache = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempCache);

        try
        {
            var location = new HtmlBrowserCacheCleaner.CacheLocation { Path = tempCache };
            using FileStream installationLock = await HtmlBrowser.AcquireInstallationFileLockAsync();
            Task<HtmlBrowserCacheCleaner.CleanResult> cleanTask = Task.Run(() =>
                HtmlBrowserCacheCleaner.CleanCache(new[] { location }));

            await Task.Delay(200);
            Assert.False(cleanTask.IsCompleted);
            Assert.True(Directory.Exists(tempCache));

            installationLock.Dispose();
            Task completedTask = await Task.WhenAny(cleanTask, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(cleanTask, completedTask);
            HtmlBrowserCacheCleaner.CleanResult result = await cleanTask;
            Assert.True(result.Success);
            Assert.False(Directory.Exists(tempCache));
        }
        finally
        {
            if (Directory.Exists(tempCache)) Directory.Delete(tempCache, true);
        }
    }

    [Fact]
    public async Task EnsureDriverInstalledAsync_DownloadsMatchingOfficialPackageWhenBundledDriverIsMissing()
    {
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var originalFactory = HtmlBrowser.HttpClientFactory;
        var handler = new FakeHandler(CreateDriverPackage());

        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
            HtmlBrowser.HttpClientFactory = () => new HttpClient(handler, disposeHandler: false);

            await HtmlBrowser.EnsureDriverInstalledAsync();

            string version = typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version?.ToString(3) ?? "1.52.0";
            Assert.Equal(
                $"https://api.nuget.org/v3-flatcontainer/microsoft.playwright/{version}/microsoft.playwright.{version}.nupkg",
                handler.LastRequestUri?.AbsoluteUri);
            Assert.True(HtmlBrowser.HasDriverLayout(Path.Combine(tempDriver, ".playwright")));
            Assert.Equal(version, File.ReadAllText(Path.Combine(tempDriver, ".playwright", ".version")));
        }
        finally
        {
            HtmlBrowser.HttpClientFactory = originalFactory;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    private static byte[] CreateDriverPackage()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            string nodeFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node";
            string platformId = PlatformExtensions.GetCurrentPlatform().ToPlatformId();
            using (var nodeStream = new StreamWriter(archive.CreateEntry($".playwright/node/{platformId}/{nodeFile}").Open()))
            {
                nodeStream.Write("node");
            }

            using (var licenseStream = new StreamWriter(archive.CreateEntry(".playwright/node/LICENSE").Open()))
            {
                licenseStream.Write("license");
            }

            using (var packageStream = new StreamWriter(archive.CreateEntry(".playwright/package/package.json").Open()))
            {
                packageStream.Write("{}");
            }

            using (var cliStream = new StreamWriter(archive.CreateEntry(".playwright/package/cli.js").Open()))
            {
                cliStream.Write("console.log('playwright');");
            }

            using (var browsersStream = new StreamWriter(archive.CreateEntry(".playwright/package/browsers.json").Open()))
            {
                browsersStream.Write("{\"browsers\":[]}");
            }
        }

        return memory.ToArray();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly byte[] _content;

        public FakeHandler(byte[] content)
        {
            _content = content;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_content)
            };
            response.Content.Headers.ContentLength = _content.Length;
            return Task.FromResult(response);
        }
    }
}
