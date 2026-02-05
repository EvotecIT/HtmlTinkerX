using HtmlTinkerX;
using System.Collections.Generic;
using System.Globalization;
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
        Assert.Equal(1704067199L, c.Expires);
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
        Assert.True(c.Expires > 1_000_000_000_000_000_000L);
    }

    [Fact]
    public void ParseNetscapeFile_ParsesCookie_WithCommaCulture() {
        CultureInfo original = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            string data = "example.com\tFALSE\t/\tTRUE\t1704067199\tsessionId\tabc123xyz";
            List<HtmlCookie> result = HtmlCookieParser.ParseNetscapeFile(data);
            Assert.Single(result);
            HtmlCookie c = result[0];
            Assert.Equal(1704067199L, c.Expires);
        }
        finally {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ToNetscapeFile_FormatsCookie() {
        List<HtmlCookie> cookies = new() {
            new HtmlCookie { Domain = "example.com", Path = "/", Secure = true, Expires = 1704067199L, Name = "sessionId", Value = "abc123xyz" }
        };
        string file = HtmlCookieParser.ToNetscapeFile(cookies);
        string[] lines = file.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("# Netscape HTTP Cookie File", lines[0]);
        string[] parts = lines[1].Split('\t');
        Assert.Equal(7, parts.Length);
        Assert.Equal("example.com", parts[0]);
        Assert.Equal("FALSE", parts[1]);
        Assert.Equal("/", parts[2]);
        Assert.Equal("TRUE", parts[3]);
        Assert.Equal("1704067199", parts[4]);
        Assert.Equal("sessionId", parts[5]);
        Assert.Equal("abc123xyz", parts[6]);
    }

    [Fact]
    public void ToNetscapeFile_RoundTrips() {
        List<HtmlCookie> cookies = new() {
            new HtmlCookie { Domain = ".example.com", Path = "/", Secure = false, Expires = 1704067200L, Name = "id", Value = "1" }
        };
        string file = HtmlCookieParser.ToNetscapeFile(cookies);
        Assert.Contains("\tTRUE\t", file);
        List<HtmlCookie> parsed = HtmlCookieParser.ParseNetscapeFile(file);
        Assert.Single(parsed);
        HtmlCookie c = parsed[0];
        Assert.Equal(".example.com", c.Domain);
        Assert.Equal("/", c.Path);
        Assert.False(c.Secure);
        Assert.Equal(1704067200L, c.Expires);
        Assert.Equal("id", c.Name);
        Assert.Equal("1", c.Value);
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
    public void ParseSetCookieHeader_ParsesSpecificDate() {
        CultureInfo original = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string header = "Set-Cookie: id=1; Path=/; Expires=Tue, 05 Mar 2024 15:00:00 GMT";
            HtmlCookie c = HtmlCookieParser.ParseSetCookieHeader(header);
            Assert.Equal(1709650800L, c.Expires);
        }
        finally {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ParseSetCookieHeader_ParsesIso8601Expiration() {
        string header = "sessionId=abc; Expires=2024-03-05T15:00:00Z";
        HtmlCookie cookie = HtmlCookieParser.ParseSetCookieHeader(header);
        Assert.Equal(1709650800L, cookie.Expires);
    }

    [Fact]
    public void ParseSetCookieHeader_ParsesUnixSecondsExpiration() {
        string header = "sessionId=abc; Expires=1709650800";
        HtmlCookie cookie = HtmlCookieParser.ParseSetCookieHeader(header);
        Assert.Equal(1709650800L, cookie.Expires);
    }

    [Fact]
    public void ParseSetCookieHeader_ParsesUnixMillisecondsExpiration() {
        string header = "sessionId=abc; Expires=1709650800000";
        HtmlCookie cookie = HtmlCookieParser.ParseSetCookieHeader(header);
        Assert.Equal(1709650800L, cookie.Expires);
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
    public void ParseOrgJsonCookie_ParsesDateRegardlessOfCulture() {
        CultureInfo original = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            string json = "{\"name\":\"id\",\"value\":\"1\",\"Expires\":\"Tue, 05 Mar 2024 15:00:00 GMT\"}";
            HtmlCookie c = HtmlCookieParser.ParseOrgJsonCookie(json);
            Assert.Equal(1709650800L, c.Expires);
        }
        finally {
            CultureInfo.CurrentCulture = original;
        }
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