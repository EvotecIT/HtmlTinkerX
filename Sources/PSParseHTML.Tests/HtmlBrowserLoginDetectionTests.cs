using System.Threading.Tasks;
using Moq;
using Microsoft.Playwright;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlBrowserLoginDetectionTests {

    [Fact]
    public async Task DetectLoginFormAsync_ReturnsSelectors() {
        var page = new Mock<IPage>();
        page.SetupGet(p => p.Url).Returns("https://example.com/login");
        page.Setup(p => p.EvaluateAsync<System.Collections.Generic.Dictionary<string, string?>>(It.IsAny<string>(), null))
            .ReturnsAsync(new System.Collections.Generic.Dictionary<string, string?> {
                ["username"] = "input#user",
                ["password"] = "input#pass",
                ["submit"] = "button#login"
            });

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
        page.Setup(p => p.EvaluateAsync<System.Collections.Generic.Dictionary<string, string?>>(It.IsAny<string>(), null))
            .ReturnsAsync((System.Collections.Generic.Dictionary<string, string?>?)null);

        HtmlFormLogin? form = await HtmlBrowser.DetectLoginFormAsync(page.Object);

        Assert.Null(form);
    }
}
