using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserCookiesTests {
    [Fact]
    public async Task GetCookiesAsync_ReturnsMappedCookies() {
        var context = new Mock<IBrowserContext>();
        var session = (HtmlBrowserSession)RuntimeHelpers.GetUninitializedObject(typeof(HtmlBrowserSession));
        typeof(HtmlBrowserSession)
            .GetField("<Context>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(session, context.Object);

        var cookies = new List<BrowserContextCookiesResult>
        {
            new BrowserContextCookiesResult
            {
                Name = "name",
                Value = "value",
                Domain = "domain",
                Path = "/path",
                Expires = 123,
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteAttribute.Lax
            }
        };
        context.Setup(c => c.CookiesAsync()).ReturnsAsync(cookies);

        List<HtmlCookie> result = await HtmlBrowser.GetCookiesAsync(session);

        Assert.Single(result);
        HtmlCookie c = result[0];
        Assert.Equal("name", c.Name);
        Assert.Equal("value", c.Value);
        Assert.Equal("domain", c.Domain);
        Assert.Equal("/path", c.Path);
        Assert.Equal(123L, c.Expires);
        Assert.True(c.HttpOnly);
        Assert.False(c.Secure);
        Assert.Equal(SameSiteAttribute.Lax, c.SameSite);
    }

    [Fact]
    public async Task SetCookiesAsync_ForwardsCookiesToAddCookiesAsync() {
        var context = new Mock<IBrowserContext>();
        var session = (HtmlBrowserSession)RuntimeHelpers.GetUninitializedObject(typeof(HtmlBrowserSession));
        typeof(HtmlBrowserSession)
            .GetField("<Context>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(session, context.Object);

        List<HtmlCookie> cookies = new()
        {
            new HtmlCookie
            {
                Name = "name",
                Value = "value",
                Url = "https://example.com",
                Domain = "domain",
                Path = "/path",
                Expires = 456,
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteAttribute.Strict
            }
        };

        context.Setup(c => c.AddCookiesAsync(It.Is<IEnumerable<Cookie>>(l =>
            l.Count() == 1 &&
            l.First().Name == "name" &&
            l.First().Value == "value" &&
            l.First().Url == "https://example.com" &&
            l.First().Domain == "domain" &&
            l.First().Path == "/path" &&
            l.First().Expires == 456 &&
            l.First().HttpOnly == true &&
            l.First().Secure == true &&
            l.First().SameSite == SameSiteAttribute.Strict
        ))).Returns(Task.CompletedTask).Verifiable();

        await HtmlBrowser.SetCookiesAsync(session, cookies);

        context.Verify();
    }

    [Fact]
    public async Task SetCookiesAsync_EmptyList_StillCallsAddCookiesAsync() {
        var context = new Mock<IBrowserContext>();
        var session = (HtmlBrowserSession)RuntimeHelpers.GetUninitializedObject(typeof(HtmlBrowserSession));
        typeof(HtmlBrowserSession)
            .GetField("<Context>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(session, context.Object);

        IEnumerable<HtmlCookie> cookies = Array.Empty<HtmlCookie>();

        context.Setup(c => c.AddCookiesAsync(It.Is<IEnumerable<Cookie>>(l => !l.Any())))
            .Returns(Task.CompletedTask).Verifiable();

        await HtmlBrowser.SetCookiesAsync(session, cookies);

        context.Verify();
    }

    [Fact]
    public async Task GetCookiesAsync_FiltersByDomain() {
        var context = new Mock<IBrowserContext>();
        var session = (HtmlBrowserSession)RuntimeHelpers.GetUninitializedObject(typeof(HtmlBrowserSession));
        typeof(HtmlBrowserSession)
            .GetField("<Context>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(session, context.Object);

        var cookies = new List<BrowserContextCookiesResult>
        {
            new BrowserContextCookiesResult
            {
                Name = "a",
                Value = "1",
                Domain = "match.com",
                Path = "/"
            },
            new BrowserContextCookiesResult
            {
                Name = "b",
                Value = "2",
                Domain = "other.com",
                Path = "/"
            }
        };
        context.Setup(c => c.CookiesAsync()).ReturnsAsync(cookies);

        List<HtmlCookie> result = await HtmlBrowser.GetCookiesAsync(session, new[] { "match.com" });

        Assert.Single(result);
        Assert.Equal("a", result[0].Name);
    }
}