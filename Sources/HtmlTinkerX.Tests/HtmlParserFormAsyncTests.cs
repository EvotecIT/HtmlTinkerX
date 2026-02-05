using HtmlTinkerX;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests asynchronous form parsing using <see cref="HtmlParser"/>.
/// </summary>
public class HtmlParserFormAsyncTests {
    private static TestServer CreateServer() {
        return TestServerCompat.CreateFormParsingTestServer();
    }

    [Fact]
    public async Task ParseUrlFormsWithAngleSharpAsync_ReturnsForms() {
        using var server = CreateServer();
        using HttpClient client = server.CreateClient();

        var forms = await HtmlParser.ParseUrlFormsWithAngleSharpAsync(server.BaseAddress + "form", client);
        Assert.Equal(2, forms.Count);
        Assert.Equal("user", forms[0].Fields[0].Name);
    }
}