using HtmlTinkerX;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NUglify.Html;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlOptimizerTests {
    [Fact]
    public void OptimizeCss_MinifiesContent() {
        const string css = "body { color: red; }";
        string result = HtmlOptimizer.OptimizeCss(css);
        Assert.Equal("body{color:#f00}", result);
    }

    [Fact]
    public void OptimizeHtml_TreatAsDocumentMinifiesContent() {
        const string input = "<html><!--c--><body> <p>Hi</p></body></html>";
        string result = HtmlOptimizer.OptimizeHtml(input, false, treatAsDocument: true, removeComments: true);
        Assert.Equal("<html><body><p>Hi</p></body></html>", result);
    }

    [Fact]
    public void OptimizeHtml_PreservesMotw() {
        const string input = "<!-- saved from url=(0014)about:internet --><html><body>test</body></html>";
        string result = HtmlOptimizer.OptimizeHtml(input, false, treatAsDocument: true);
        Assert.StartsWith("<!-- saved from url=(0014)about:internet -->", result);
    }

    [Fact]
    public void OptimizeHtml_DoesNotWrapFragmentsByDefault() {
        const string input = "<tr></tr>";
        string result = HtmlOptimizer.OptimizeHtml(input, false);
        Assert.Equal("<tr></tr>", result);
    }

    [Fact]
    public void OptimizeHtml_PreservesCommentsByDefault() {
        const string input = "<html><!--c--><body>Hi</body></html>";
        string result = HtmlOptimizer.OptimizeHtml(input, false, treatAsDocument: true);
        Assert.Contains("<!--c-->", result);
    }

    [Fact]
    public void OptimizeHtml_RemoveCommentsWhenRequested() {
        const string input = "<html><!--c--><body>Hi</body></html>";
        string result = HtmlOptimizer.OptimizeHtml(input, false, treatAsDocument: true, removeComments: true);
        Assert.DoesNotContain("<!--c-->", result);
    }

    [Fact]
    public void OptimizeHtml_RemoveOptionalTagsWhenRequested() {
        const string input = "<html><body><p>Hi</p></body></html>";
        string result = HtmlOptimizer.OptimizeHtml(input, false, treatAsDocument: true, removeOptionalTags: true);
        Assert.DoesNotContain("</p>", result);
    }

    [Fact]
    public void OptimizeHtml_PreservesLiteralTrueValuesByDefault() {
        const string input = "<a hx-boost=true></a>";
        string result = HtmlOptimizer.OptimizeHtml(input, cssDecodeEscapes: false);

        Assert.Equal("<a hx-boost=true></a>", result);
    }

    [Fact]
    public void OptimizeHtml_ShortBooleanAttributesWhenRequested() {
        const string hxInput = "<a hx-boost=true></a>";
        string defaultResult = HtmlOptimizer.OptimizeHtml(hxInput, cssDecodeEscapes: false);
        string hxShortened = HtmlOptimizer.OptimizeHtml(
            hxInput,
            cssDecodeEscapes: false,
            shortBooleanAttributes: true);

        const string booleanInput = "<input type=\"checkbox\" checked=\"checked\" />";
        string booleanShortened = HtmlOptimizer.OptimizeHtml(
            booleanInput,
            cssDecodeEscapes: false,
            shortBooleanAttributes: true);

        Assert.Equal("<a hx-boost=true></a>", defaultResult);
        Assert.Equal("<a hx-boost></a>", hxShortened);
        Assert.Equal("<input type=checkbox checked />", booleanShortened);
    }

    [Fact]
    public void CreateDefaultHtmlSettings_UsesSafeDefaults() {
        HtmlSettings settings = HtmlOptimizer.CreateDefaultHtmlSettings();

        Assert.False(settings.RemoveComments);
        Assert.False(settings.RemoveOptionalTags);
        Assert.True(settings.IsFragmentOnly);
        Assert.False(settings.ShortBooleanAttribute);
        Assert.False(settings.CssSettings.DecodeEscapes);
    }

    [Fact]
    public void OptimizeHtml_WithCustomSettingsHonorsNuGlifyOptions() {
        const string input = "<div hidden=\"hidden\" data-flag=\"value\"><!--comment--></div>";
        HtmlSettings settings = HtmlOptimizer.CreateDefaultHtmlSettings();
        settings.RemoveComments = true;
        settings.ShortBooleanAttribute = true;
        settings.RemoveAttributeQuotes = false;
        settings.AlphabeticallyOrderAttributes = true;

        string result = HtmlOptimizer.OptimizeHtml(input, settings);

        Assert.DoesNotContain("<!--comment-->", result);
        Assert.Contains("hidden", result);
        Assert.Contains("data-flag=\"value\"", result);
        Assert.StartsWith("<div", result);
    }

    [Fact]
    public void OptimizeJavaScript_MinifiesInput() {
        const string formatted = "(function() {\n    function main() {\n        var tabButtons = [].slice.call(document.querySelectorAll('ul.tab-nav li a.buttonTab'));\n        tabButtons.map(function(button) {\n            button.addEventListener('click', function() {\n                document.querySelector('li a.active.buttonTab').classList.remove('active');\n                button.classList.add('active');\n                document.querySelector('.tab-pane.active').classList.remove('active');\n                document.querySelector(button.getAttribute('href')).classList.add('active');\n            });\n        });\n    }\n    if (document.readyState !== 'loading') {\n        main();\n    } else {\n        document.addEventListener('DOMContentLoaded', main);\n    }\n})();";

        const string expected = "(function(){function n(){var n=[].slice.call(document.querySelectorAll(\"ul.tab-nav li a.buttonTab\"));n.map(function(n){n.addEventListener(\"click\",function(){document.querySelector(\"li a.active.buttonTab\").classList.remove(\"active\");n.classList.add(\"active\");document.querySelector(\".tab-pane.active\").classList.remove(\"active\");document.querySelector(n.getAttribute(\"href\")).classList.add(\"active\")})})}document.readyState!==\"loading\"?n():document.addEventListener(\"DOMContentLoaded\",n)})()";

        string result = HtmlOptimizer.OptimizeJavaScript(formatted);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task EmbedImagesAsDataUriAsync_ReplacesImageSources() {
        var builder = new WebHostBuilder()
            .Configure(app => app.Run(async ctx => {
                byte[] img = System.Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAAWgmWQ0AAAAASUVORK5CYII=");
                ctx.Response.ContentType = "image/png";
                await ctx.Response.Body.WriteAsync(img, 0, img.Length);
            }));
        using var server = new TestServer(builder);
        using HttpClient client = server.CreateClient();
        string html = $"<html><body><img src=\"{server.BaseAddress}image.png\" /></body></html>";

        string result = await HtmlOptimizer.EmbedImagesAsDataUriAsync(html, client: client);

        Assert.Contains("data:image/png;base64", result);
        Assert.DoesNotContain("image.png", result);
    }
}