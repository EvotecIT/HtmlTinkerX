using HtmlTinkerX;
using Xunit;
using System.Threading.Tasks;

namespace PSParseHTML.Tests;

/// <summary>
/// Tests asynchronous formatting helpers in <see cref="HtmlFormatter"/>.
/// </summary>
public class HtmlFormatterAsyncTests {
    [Fact]
    /// <summary>
    /// Ensures that minified HTML is expanded asynchronously.
    /// </summary>
    public async Task FormatHtmlAsync_FormatsMinifiedHtml() {
        const string input = "<html><body><div><p>Test</p></div></body></html>";
        string result = await HtmlFormatter.FormatHtmlAsync(input);
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.NotEqual(input, result);
    }

    [Fact]
    /// <summary>
    /// Asynchronously formats minified CSS.
    /// </summary>
    public async Task FormatCssAsync_FormatsMinifiedCss() {
        const string content = ".tabsWrapper{text-align:center;margin:10px auto;font-family:\"Roboto\", sans-serif!important}.tabs{margin-top:10px;font-size:15px;padding:0;list-style:none;background:rgba(255, 255, 255, 1);box-shadow:0 5px 20px rgba(0, 0, 0, 0.1);border-radius:4px;position:relative}.tabs .round{border-radius:4px}.tabs a{text-decoration:none;color:rgba(119, 119, 119, 1);text-transform:uppercase;padding:10px 20px;display:inline-block;position:relative;z-index:1;transition-duration:0.6s}.tabs a.active{color:rgba(255, 255, 255, 1)}.tabs a i{margin-right:5px}.tabs .selector{display:none;height:100%;position:absolute;left:0;top:0;right:0;bottom:0;z-index:1;border-radius:4px}.tabs-content{display:none}.tabs-content.active{display:block}";
        string result = await HtmlFormatter.FormatCssAsync(content);
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.NotEqual(content, result);
    }
}
