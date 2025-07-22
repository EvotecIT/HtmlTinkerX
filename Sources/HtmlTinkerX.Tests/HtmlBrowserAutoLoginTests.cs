using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserAutoLoginTests {
    [Fact]
    public async Task AutoLoginAsync_FillsAndSubmitsForm() {
        string html = "<form><input type='text' id='u'/><input type='password' id='p'/><button id='s'></button></form>";
        var page = new Mock<IPage>();
        page.SetupGet(p => p.Url).Returns("https://example.com/login");
        page.Setup(p => p.ContentAsync()).ReturnsAsync(html);

        var user = new Mock<ILocator>();
        user.Setup(l => l.WaitForAsync(It.IsAny<LocatorWaitForOptions?>())).Returns(Task.CompletedTask).Verifiable();
        user.Setup(l => l.FillAsync("user", It.IsAny<LocatorFillOptions?>())).Returns(Task.CompletedTask).Verifiable();
        var pass = new Mock<ILocator>();
        pass.Setup(l => l.WaitForAsync(It.IsAny<LocatorWaitForOptions?>())).Returns(Task.CompletedTask).Verifiable();
        pass.Setup(l => l.FillAsync("pass", It.IsAny<LocatorFillOptions?>())).Returns(Task.CompletedTask).Verifiable();
        var submit = new Mock<ILocator>();
        submit.Setup(l => l.WaitForAsync(It.IsAny<LocatorWaitForOptions?>())).Returns(Task.CompletedTask).Verifiable();
        submit.Setup(l => l.ClickAsync(It.IsAny<LocatorClickOptions?>())).Returns(Task.CompletedTask).Verifiable();

        page.Setup(p => p.Locator("input#u", It.IsAny<PageLocatorOptions?>())).Returns(user.Object).Verifiable();
        page.Setup(p => p.Locator("input#p", It.IsAny<PageLocatorOptions?>())).Returns(pass.Object).Verifiable();
        page.Setup(p => p.Locator("button#s", It.IsAny<PageLocatorOptions?>())).Returns(submit.Object).Verifiable();

        bool result = await HtmlBrowser.AutoLoginAsync(page.Object, "user", "pass");

        Assert.True(result);
        page.Verify();
        user.Verify();
        pass.Verify();
        submit.Verify();
    }

    [Fact]
    public async Task AutoLoginAsync_ReturnsFalseWhenNoForm() {
        var page = new Mock<IPage>();
        page.SetupGet(p => p.Url).Returns("https://example.com/");
        page.Setup(p => p.ContentAsync()).ReturnsAsync("<div></div>");

        bool result = await HtmlBrowser.AutoLoginAsync(page.Object, "u", "p");

        Assert.False(result);
        page.Verify(p => p.Locator(It.IsAny<string>(), It.IsAny<PageLocatorOptions?>()), Times.Never());
    }
}
