using System.Threading.Tasks;
using Moq;
using Microsoft.Playwright;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlBrowserLoginDetectionTests {

    [Fact]
    public async Task DetectLoginFormAsync_ReturnsSelectors() {
        string markup = "<form><input type='text' id='user'/><input type='password' id='pass'/><button id='login'></button></form>";
        var page = new Mock<IPage>();
        page.SetupGet(p => p.Url).Returns("https://example.com/login");
        page.Setup(p => p.ContentAsync()).ReturnsAsync(markup);

        HtmlFormLogin? form = await HtmlBrowser.DetectLoginFormAsync(page.Object);

        Assert.NotNull(form);
        Assert.Equal("https://example.com/login", form!.LoginUrl);
        Assert.Equal("input#user", form.UsernameSelector);
        Assert.Equal("input#pass", form.PasswordSelector);
        Assert.Equal("button#login", form.SubmitSelector);
    }

    [Fact]
    public async Task DetectLoginFormAsync_ReturnsNullWhenMissing() {
        var page = new Mock<IPage>();
        page.Setup(p => p.ContentAsync()).ReturnsAsync("<div></div>");

        HtmlFormLogin? form = await HtmlBrowser.DetectLoginFormAsync(page.Object);

        Assert.Null(form);
    }

    [Fact]
    public void DetectLoginForm_FromHtml() {
        string html = "<form><input type='text' id='u'/><input type='password' id='p'/><button id='s'></button></form>";
        HtmlFormLogin? form = HtmlLoginParser.Detect(html, "https://example.com/login");

        Assert.NotNull(form);
        Assert.Equal("https://example.com/login", form!.LoginUrl);
        Assert.Equal("input#u", form.UsernameSelector);
        Assert.Equal("input#p", form.PasswordSelector);
        Assert.Equal("button#s", form.SubmitSelector);
    }

    [Fact]
    public void DetectLoginForm_IgnoresHiddenPassword() {
        string html = "<form>" +
            "<input type='text' id='u'/>" +
            "<input type='password' id='hidden' style='display:none'/>" +
            "<input type='password' id='p'/>" +
            "<button id='s'></button>" +
            "</form>";

        HtmlFormLogin? form = HtmlLoginParser.Detect(html, "https://example.com/login");

        Assert.NotNull(form);
        Assert.Equal("input#p", form!.PasswordSelector);
    }

    [Fact]
    public void DetectLoginForm_EscapesQuotesInAttributes() {
        string html = "<form>" +
            "<input type='text' name=\"user'name\"/>" +
            "<input type='password' id='p\"ass'/>" +
            "<button id='login'></button>" +
            "</form>";

        HtmlFormLogin? form = HtmlLoginParser.Detect(html, "https://example.com/login");

        Assert.NotNull(form);
        Assert.Equal("input[name='user\\'name']", form!.UsernameSelector);
        Assert.Equal("input#p\\\"ass", form.PasswordSelector);
    }
}
