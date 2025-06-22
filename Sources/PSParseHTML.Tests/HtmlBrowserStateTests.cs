using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlBrowserStateTests {
    [Fact]
    public async Task ExportBrowserStateAsync_CreatesDirectoryAndCallsPlaywright() {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string file = Path.Combine(dir, "state.json");
        var context = new Mock<IBrowserContext>();
        context.Setup(c => c.StorageStateAsync(It.Is<BrowserContextStorageStateOptions>(o => o.Path == file)))
            .ReturnsAsync("{}").Verifiable();
        var session = (HtmlBrowserSession)FormatterServices.GetUninitializedObject(typeof(HtmlBrowserSession));
        typeof(HtmlBrowserSession).GetProperty("Context")!.SetValue(session, context.Object);

        await HtmlBrowser.ExportBrowserStateAsync(session, file);

        Assert.True(Directory.Exists(dir));
        context.Verify();
        Directory.Delete(dir, true);
    }
}
