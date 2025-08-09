using HtmlTinkerX;
using System.Collections.Generic;
using Microsoft.Playwright;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlCookieParserTests {
    [Fact]
    public void ParseNetscapeFile_ParsesCookie() {
        string data = "example.com\tFALSE\t/\tTRUE\t1704067199\tsessionId\tabc123xyz";
        List<HtmlCookie> result = HtmlCookieParser.ParseNetscapeFile(data);
        Assert.Single(result);
        HtmlCookie c = result[0];
        Assert.Equal("sessionId", c.Name);
        Assert.Equal("abc123xyz", c.Value);
        Assert.Equal("example.com", c.Domain);
        Assert.Equal("/", c.Path);
        Assert.True(c.Secure);
        Assert.Equal(1704067199d, c.Expires);
    }

    [Fact]
    public void ParseNetscapeFile_ParsesLargeExpiration() {
        string data = "example.com\tFALSE\t/\tTRUE\t9223372036854775807\tsessionId\tabc123xyz";
        List<HtmlCookie> result = HtmlCookieParser.ParseNetscapeFile(data);
        Assert.Single(result);
        HtmlCookie c = result[0];
        Assert.Equal("sessionId", c.Name);
        Assert.Equal("abc123xyz", c.Value);
        Assert.Equal("example.com", c.Domain);
        Assert.Equal("/", c.Path);
        Assert.True(c.Secure);
        Assert.True(c.Expires > 1e18d);
    }

    [Fact]
    public void ParseSetCookieHeader_ParsesCookie() {
        string header = "Set-Cookie: sessionId=abc123xyz; Path=/; Secure; Expires=Wed, 31 Jan 2024 23:59:59 GMT";
        HtmlCookie c = HtmlCookieParser.ParseSetCookieHeader(header);
        Assert.Equal("sessionId", c.Name);
        Assert.Equal("abc123xyz", c.Value);
        Assert.Equal("/", c.Path);
        Assert.True(c.Secure);
        Assert.NotNull(c.Expires);
    }

    [Fact]
    public void ParseOrgJsonCookie_ParsesJson() {
        string json = "{\"Path\":\"/\",\"Secure\":\"false\",\"name\":\"sessionId\",\"Expires\":\"Sun, 31 Dec 2023 23:59:59 GMT\",\"Domain\":\"example.com\",\"value\":\"abc123xyz\"}";
        HtmlCookie c = HtmlCookieParser.ParseOrgJsonCookie(json);
        Assert.Equal("sessionId", c.Name);
        Assert.Equal("abc123xyz", c.Value);
        Assert.Equal("example.com", c.Domain);
    }

    [Fact]
    public void ParseCookieStoreJson_ParsesJson() {
        string json = "{\"domain\":null,\"expires\":1704067199000,\"name\":\"sessionId\",\"partitioned\":false,\"path\":\"/\",\"sameSite\":\"lax\",\"secure\":true,\"value\":\"abc123xyz\"}";
        HtmlCookie c = HtmlCookieParser.ParseCookieStoreJson(json);
        Assert.Equal("sessionId", c.Name);
        Assert.Equal("abc123xyz", c.Value);
        Assert.Equal("/", c.Path);
        Assert.True(c.Secure);
        Assert.Equal(SameSiteAttribute.Lax, c.SameSite);
    }

    [Fact]
    public void ParsePuppeteerJson_ParsesArray() {
        string json = "[{\"name\":\"sessionId\",\"value\":\"abc123xyz\",\"domain\":\"example.com\",\"path\":\"/\",\"expires\":1704067199,\"httpOnly\":false,\"secure\":false}]";
        List<HtmlCookie> list = HtmlCookieParser.ParsePuppeteerJson(json);
        Assert.Single(list);
        HtmlCookie c = list[0];
        Assert.Equal("sessionId", c.Name);
        Assert.Equal("abc123xyz", c.Value);
        Assert.Equal("example.com", c.Domain);
    }

    [Fact]
    public void ParseOrgJsonCookie_AllowsLowercaseProperties() {
        string json = "{\"path\":\"/\",\"secure\":\"true\",\"name\":\"sessionId\",\"httponly\":\"true\",\"domain\":\"example.com\",\"value\":\"abc123xyz\"}";
        HtmlCookie c = HtmlCookieParser.ParseOrgJsonCookie(json);
        Assert.True(c.Secure);
        Assert.True(c.HttpOnly);
    }

    [Fact]
    public void ParseOrgJsonCookie_AllowsMixedCaseProperties() {
        string json = "{\"Path\":\"/\",\"SeCuRe\":\"true\",\"name\":\"sessionId\",\"HtTpOnLy\":\"true\",\"Domain\":\"example.com\",\"value\":\"abc123xyz\"}";
        HtmlCookie c = HtmlCookieParser.ParseOrgJsonCookie(json);
        Assert.True(c.Secure);
        Assert.True(c.HttpOnly);
    }
}