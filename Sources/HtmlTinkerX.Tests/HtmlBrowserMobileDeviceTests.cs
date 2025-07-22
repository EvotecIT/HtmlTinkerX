using HtmlTinkerX;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserMobileDeviceTests {
    [Fact]
    public void GetMobileDeviceInfo_ReturnsExpectedInfo() {
        HtmlMobileDeviceInfo info = HtmlBrowser.GetMobileDeviceInfo(HtmlMobileDevice.Pixel5);
        Assert.Contains("Pixel 5", info.UserAgent);
        Assert.Equal(393, info.ViewportWidth);
        Assert.Equal(851, info.ViewportHeight);
    }
}
