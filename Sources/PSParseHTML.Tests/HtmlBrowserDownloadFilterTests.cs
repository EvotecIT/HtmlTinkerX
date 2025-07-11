using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using Microsoft.Playwright;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlBrowserDownloadFilterTests {
    [Fact]
    public async Task SavePageDownloadsAsync_FiltersDownloadsByFilename() {
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
                    .Callback<string>(p => File.WriteAllText(p, "file1"))
                    .Returns(Task.CompletedTask);

                var dl2 = new Mock<IDownload>();
                dl2.SetupGet(d => d.Url).Returns("https://example.com/other.bin");
                dl2.SetupGet(d => d.SuggestedFilename).Returns("other.bin");
                dl2.Setup(d => d.SaveAsAsync(It.IsAny<string>()))
                    .Callback<string>(p => File.WriteAllText(p, "other"))
                    .Returns(Task.CompletedTask);

                page.Raise(p => p.Download += null!, page.Object, dl1.Object);
                page.Raise(p => p.Download += null!, page.Object, dl2.Object);
            })
            .ReturnsAsync((JsonElement?)default);
        page.Setup(p => p.WaitForLoadStateAsync(It.IsAny<LoadState>(), It.IsAny<PageWaitForLoadStateOptions?>()))
            .Returns(Task.CompletedTask);

        var files = new List<string>();
        await foreach (string f in HtmlBrowser.SavePageDownloadsAsync(page.Object, dir, "file1")) {
            files.Add(f);
        }

        string path1 = Path.Combine(dir, "file1.txt");
        string path2 = Path.Combine(dir, "other.bin");

        Assert.Single(files);
        Assert.Contains(path1, files);
        Assert.True(File.Exists(path1));
        Assert.False(File.Exists(path2));

        Directory.Delete(dir, true);
    }
}
