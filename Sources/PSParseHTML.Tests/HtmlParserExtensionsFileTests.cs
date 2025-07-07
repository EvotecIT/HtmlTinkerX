using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PSParseHTML.Tests;

/// <summary>
/// Tests extension methods of <see cref="HtmlParser"/> that work with files.
/// </summary>
public class HtmlParserExtensionsFileTests {
    private static string GetPath(string name) => TestHelpers.GetDocumentPath(name);

    [Fact]
    public void GetElements_EmptyHtml_ReturnsEmpty() {
        var elements = HtmlParserExtensions.GetElements(string.Empty, tag: "p");
        Assert.Empty(elements);
    }

    [Fact]
    public void GetElementsFromFile_ByTag_ReturnsElements() {
        string path = GetPath("sample_form.html");
        var elements = HtmlParserExtensions.GetElementsFromFile(path, tag: "form").ToArray();
        Assert.Equal(2, elements.Length);
    }

    [Fact]
    public void GetElementsFromFile_TempHtml_ReturnsExpectedCounts() {
        const string html = "<div id='main' class='wrapper'><span class='item'>A</span><span class='item'>B</span><p id='para' name='para'>C</p></div>";
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".html");
        File.WriteAllText(path, html);
        try {
            Assert.Equal(2, HtmlParserExtensions.GetElementsFromFile(path, tag: "span").Count());
            Assert.Equal(2, HtmlParserExtensions.GetElementsFromFile(path, className: "item").Count());
            Assert.Single(HtmlParserExtensions.GetElementsFromFile(path, id: "para"));
            Assert.Single(HtmlParserExtensions.GetElementsFromFile(path, name: "para"));
        } finally {
            File.Delete(path);
        }
    }
}
