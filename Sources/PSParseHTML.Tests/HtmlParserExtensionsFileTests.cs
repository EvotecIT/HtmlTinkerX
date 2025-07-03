using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PSParseHTML.Tests;

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
}
