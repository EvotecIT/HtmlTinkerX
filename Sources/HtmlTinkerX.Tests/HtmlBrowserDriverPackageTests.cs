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
    public async Task CleanInstallationAsync_WaitsForActiveInstallerAndPreservesDriverSearchRoot()
    {
        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string driverPath = Path.Combine(tempDriver, ".playwright");
        string siblingPath = Path.Combine(tempDriver, "application.dll");
        string? originalBrowsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");

        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
            Directory.CreateDirectory(tempBrowsers);
            Directory.CreateDirectory(driverPath);
            File.WriteAllText(siblingPath, "caller-owned application");

            using FileStream installationLock = await HtmlBrowser.AcquireInstallationFileLockAsync();
            Task cleanTask = Task.Run(async () => await HtmlBrowser.CleanInstallationAsync());

            await Task.Delay(200);
            Assert.False(cleanTask.IsCompleted);
            Assert.True(Directory.Exists(tempBrowsers));
            Assert.True(Directory.Exists(tempDriver));
            Assert.True(Directory.Exists(driverPath));

            installationLock.Dispose();
            Task completedTask = await Task.WhenAny(cleanTask, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(cleanTask, completedTask);
            await cleanTask;
            Assert.False(Directory.Exists(tempBrowsers));
            Assert.False(Directory.Exists(driverPath));
            Assert.True(Directory.Exists(tempDriver));
            Assert.Equal("caller-owned application", File.ReadAllText(siblingPath));
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

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    public async Task EnsureDriverInstalledAsync_DownloadsMatchingOfficialPackageWithinSearchRoot(
        bool explicitDriverPath, bool trailingSeparator, bool lookalikeRoot)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(tempRoot, lookalikeRoot ? "application.playwright" : "application");
        string driverPath = Path.Combine(tempDriver, ".playwright");
        string searchPath = explicitDriverPath ? driverPath : tempDriver;
        if (trailingSeparator) searchPath += Path.DirectorySeparatorChar;
        string siblingPath = Path.Combine(tempDriver, "application.dll");
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var originalFactory = HtmlBrowser.HttpClientFactory;
        var handler = new FakeHandler(CreateDriverPackage());

        try
        {
            Directory.CreateDirectory(tempDriver);
            File.WriteAllText(siblingPath, "caller-owned application");
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", searchPath);
            HtmlBrowser.HttpClientFactory = () => new HttpClient(handler, disposeHandler: false);

            await HtmlBrowser.EnsureDriverInstalledAsync();

            string version = typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version?.ToString(3) ?? "1.52.0";
            Assert.Equal(
                $"https://api.nuget.org/v3-flatcontainer/microsoft.playwright/{version}/microsoft.playwright.{version}.nupkg",
                handler.LastRequestUri?.AbsoluteUri);
            Assert.True(HtmlBrowser.HasDriverLayout(driverPath));
            Assert.Equal(version, File.ReadAllText(Path.Combine(driverPath, ".version")));
            Assert.Equal("caller-owned application", File.ReadAllText(siblingPath));
        }
        finally
        {
            HtmlBrowser.HttpClientFactory = originalFactory;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CleanDriver_RemovesOnlyOwnedChild(bool explicitDriverPath)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string searchRoot = Path.Combine(tempRoot, "application");
        string driverPath = Path.Combine(searchRoot, ".playwright");
        string siblingPath = Path.Combine(searchRoot, "settings", "application.json");
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");

        try
        {
            Directory.CreateDirectory(driverPath);
            Directory.CreateDirectory(Path.GetDirectoryName(siblingPath)!);
            File.WriteAllText(Path.Combine(driverPath, "corrupted-driver"), "disposable driver");
            File.WriteAllText(siblingPath, "caller-owned settings");
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", explicitDriverPath ? driverPath : searchRoot);

            HtmlBrowser.CleanDriver();

            Assert.False(Directory.Exists(driverPath));
            Assert.True(Directory.Exists(searchRoot));
            Assert.Equal("caller-owned settings", File.ReadAllText(siblingPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.OK)]
    public async Task EnsureDriverInstalledAsync_PreservesSearchRootWhenDownloadOrExtractionFails(HttpStatusCode statusCode)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string siblingPath = Path.Combine(tempRoot, "application.dll");
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var originalFactory = HtmlBrowser.HttpClientFactory;
        var handler = new FakeHandler(new byte[] { 0, 1, 2 }, statusCode);

        try
        {
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(siblingPath, "caller-owned application");
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempRoot);
            HtmlBrowser.HttpClientFactory = () => new HttpClient(handler, disposeHandler: false);

            if (statusCode == HttpStatusCode.OK)
                await Assert.ThrowsAsync<InvalidDataException>(() => HtmlBrowser.EnsureDriverInstalledAsync());
            else
                await Assert.ThrowsAsync<HttpRequestException>(() => HtmlBrowser.EnsureDriverInstalledAsync());

            Assert.False(Directory.Exists(Path.Combine(tempRoot, ".playwright")));
            Assert.True(Directory.Exists(tempRoot));
            Assert.Equal("caller-owned application", File.ReadAllText(siblingPath));
        }
        finally
        {
            HtmlBrowser.HttpClientFactory = originalFactory;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
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
        private readonly HttpStatusCode _statusCode;

        public FakeHandler(byte[] content, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _content = content;
            _statusCode = statusCode;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new ByteArrayContent(_content)
            };
            response.Content.Headers.ContentLength = _content.Length;
            return Task.FromResult(response);
        }
    }
}
