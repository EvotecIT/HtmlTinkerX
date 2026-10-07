using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserStateTests {
    private const string StateJson = "{\"cookies\":[],\"origins\":[{\"origin\":\"https://example.test\",\"localStorage\":[{\"name\":\"locale\",\"value\":\"zażółć\"}]}]}";

    [Theory]
    [InlineData(false, "state.json", false)]
    [InlineData(true, "state.json", true)]
    [InlineData(false, "state", false)]
    public async Task ExportState_WritesCompleteUtf8File(bool alias, string fileName, bool overwrite) {
        string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string file = Path.Combine(directory, fileName);
        Mock<IBrowserContext> context = new();
        context.Setup(value => value.StorageStateAsync(It.IsAny<BrowserContextStorageStateOptions?>()))
            .Returns((BrowserContextStorageStateOptions? options) => {
                if (options?.Path != null) File.WriteAllText(options.Path, StateJson);
                return Task.FromResult(StateJson);
            });
        await using HtmlBrowserSession session = CreateSession(context);
        try {
            if (overwrite) {
                Directory.CreateDirectory(directory);
                File.WriteAllText(file, "previous state");
            }
            await ExportAsync(session, file, alias);

            Assert.Equal(new UTF8Encoding(false).GetBytes(StateJson), File.ReadAllBytes(file));
            Assert.Equal(new[] { file }, Directory.GetFiles(directory));
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportState_PreCancelledRequestDoesNotCreateOutputOrReadDriver(bool alias) {
        string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Mock<IBrowserContext> context = new();
        await using HtmlBrowserSession session = CreateSession(context);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        try {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExportAsync(session, Path.Combine(directory, "state.json"), alias, cancellation.Token));

            Assert.False(Directory.Exists(directory));
            context.Verify(value => value.StorageStateAsync(It.IsAny<BrowserContextStorageStateOptions?>()), Times.Never);
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    public async Task ExportState_CancellationStopsWaitingAndLateReadCannotWriteOutput(bool alias, bool existingFile, bool video) {
        string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string file = Path.Combine(directory, "state.json");
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<string>? driverRead = null;
        string? driverOutputPath = null;
        Mock<IBrowserContext> context = new();
        Mock<IPage> page = new();
        Mock<IBrowser> browser = new();
        context.Setup(value => value.StorageStateAsync(It.IsAny<BrowserContextStorageStateOptions?>()))
            .Returns((BrowserContextStorageStateOptions? options) => {
                entered.TrySetResult(true);
                driverOutputPath = options?.Path;
                driverRead = ReadStateAsync(options);
                return driverRead;
            });
        async Task<string> ReadStateAsync(BrowserContextStorageStateOptions? options) {
            string state = await pending.Task.ConfigureAwait(false);
            if (options?.Path != null) File.WriteAllText(options.Path, state);
            return state;
        }
        await using HtmlBrowserSession session = new(new Mock<IPlaywright>().Object, browser.Object, context.Object, page.Object);
        using CancellationTokenSource cancellation = new();
        Directory.CreateDirectory(directory);
        if (existingFile) File.WriteAllText(file, "previous state");
        Task operation = video
            ? HtmlBrowser.StartVideoRecordingAsync(session, Path.Combine(directory, "video.webm"), cancellationToken: cancellation.Token)
            : ExportAsync(session, file, alias, cancellation.Token);
        try {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, Task.Delay(TimeSpan.FromSeconds(2))));
            cancellation.Cancel();
            Assert.Same(operation, await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(2))));
            OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.NotNull(driverRead);
            Assert.False(driverRead.IsCompleted);
            page.Verify(value => value.CloseAsync(It.IsAny<PageCloseOptions?>()), Times.Never);
            context.Verify(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()), Times.Never);
            browser.Verify(value => value.CloseAsync(It.IsAny<BrowserCloseOptions?>()), Times.Never);

            pending.SetResult(StateJson);
            await driverRead;
            if (video && driverOutputPath != null) Assert.False(File.Exists(driverOutputPath));
            if (existingFile) Assert.Equal("previous state", File.ReadAllText(file));
            else Assert.False(File.Exists(file));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        } finally {
            pending.TrySetResult(StateJson);
            try { await operation; } catch (OperationCanceledException) { }
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task StartVideoRecording_PreCancelledRequestDoesNotReadDriver() {
        Mock<IBrowserContext> context = new();
        context.Setup(value => value.StorageStateAsync(It.IsAny<BrowserContextStorageStateOptions?>())).ReturnsAsync(StateJson);
        await using HtmlBrowserSession session = CreateSession(context);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HtmlBrowser.StartVideoRecordingAsync(session, "video.webm", cancellationToken: cancellation.Token));
        context.Verify(value => value.StorageStateAsync(It.IsAny<BrowserContextStorageStateOptions?>()), Times.Never);
    }

    [Fact]
    public async Task ExportState_DriverFailurePreservesExistingFile() {
        string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string file = Path.Combine(directory, "state.json");
        Mock<IBrowserContext> context = new();
        context.Setup(value => value.StorageStateAsync(It.IsAny<BrowserContextStorageStateOptions?>()))
            .ThrowsAsync(new PlaywrightException("State read failed"));
        await using HtmlBrowserSession session = CreateSession(context);
        Directory.CreateDirectory(directory);
        File.WriteAllText(file, "previous state");
        try {
            await Assert.ThrowsAsync<PlaywrightException>(() => ExportAsync(session, file, alias: false));
            Assert.Equal("previous state", File.ReadAllText(file));
            Assert.Equal(new[] { file }, Directory.GetFiles(directory));
        } finally {
            Directory.Delete(directory, true);
        }
    }

    private static HtmlBrowserSession CreateSession(Mock<IBrowserContext> context) =>
        new(new Mock<IPlaywright>().Object, new Mock<IBrowser>().Object, context.Object, new Mock<IPage>().Object);

    private static Task ExportAsync(HtmlBrowserSession session, string file, bool alias, CancellationToken token = default) =>
        alias ? HtmlBrowser.ExportBrowserStateAsync(session, file, token) : HtmlBrowser.ExportSessionAsync(session, file, token);
}
