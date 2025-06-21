using System.Runtime.Serialization;
using PSParseHTML;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlBrowserSessionDisposeTests {
    [Fact]
    public async Task DisposeAsync_AllowsNullProperties() {
        var session = (HtmlBrowserSession)FormatterServices.GetUninitializedObject(typeof(HtmlBrowserSession));
        await session.DisposeAsync();
    }
}
