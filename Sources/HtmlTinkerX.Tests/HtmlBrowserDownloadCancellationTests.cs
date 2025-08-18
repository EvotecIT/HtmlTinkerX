using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserDownloadCancellationTests {
    [Fact]
    public async Task SavePageDownloadsAsync_CancelEnumeration_WaitsForPendingDownloads() {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var page = new Mock<IPage>();
        page.Setup(p => p.QuerySelectorAllAsync(It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<IElementHandle>());
        page.Setup(p => p.WaitForSelectorAsync(It.IsAny<string>(), It.IsAny<PageWaitForSelectorOptions?>()))
            .ReturnsAsync((IElementHandle?)null);
        page.Setup(p => p.EvaluateAsync(It.IsAny<string>(), It.IsAny<object?>()))
            .Callback(() => {
                var dl1 = new Mock<IDownload>();
                dl1.SetupGet(d => d.Url).Returns("https://example.com/file1.txt");
                dl1.SetupGet(d => d.SuggestedFilename).Returns("file1.txt");
                dl1.Setup(d => d.SaveAsAsync(It.IsAny<string>()))
                    .Returns<string>(async p => {
                        await Task.Delay(50);
                        File.WriteAllText(p, "file1");
                    });

                var dl2 = new Mock<IDownload>();
                dl2.SetupGet(d => d.Url).Returns("https://example.com/file2.txt");
                dl2.SetupGet(d => d.SuggestedFilename).Returns("file2.txt");
                dl2.Setup(d => d.SaveAsAsync(It.IsAny<string>()))
                    .Returns<string>(async p => {
                        await Task.Delay(50);
                        File.WriteAllText(p, "file2");
                    });

                page.Raise(p => p.Download += (_, _) => { }, page.Object, dl1.Object);
                page.Raise(p => p.Download += (_, _) => { }, page.Object, dl2.Object);
            })
            .ReturnsAsync((JsonElement?)default);
        page.Setup(p => p.WaitForLoadStateAsync(It.IsAny<LoadState>(), It.IsAny<PageWaitForLoadStateOptions?>()))
            .Returns(Task.CompletedTask);

        await using var enumerator = HtmlBrowser.SavePageDownloadsAsync(page.Object, dir).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        string first = enumerator.Current;
        await enumerator.DisposeAsync();

        string path1 = Path.Combine(dir, "file1.txt");
        string path2 = Path.Combine(dir, "file2.txt");

        Assert.True(File.Exists(path1));
        Assert.True(File.Exists(path2));
        Assert.Equal("file1", File.ReadAllText(path1));
        Assert.Equal("file2", File.ReadAllText(path2));

        Directory.Delete(dir, true);
    }
}
