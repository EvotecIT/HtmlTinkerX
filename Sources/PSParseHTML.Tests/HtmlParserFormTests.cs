using System.IO;
using Xunit;

namespace PSParseHTML.Tests;

/// <summary>
/// Tests parsing of HTML forms using <see cref="HtmlParser"/>.
/// </summary>
public class HtmlParserFormTests {
    private static string GetSampleFormHtml() {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", "sample_form.html"));
        return File.ReadAllText(path);
    }

    [Fact]
    /// <summary>
    /// Validates that form parsing returns expected metadata.
    /// </summary>
    public void ParseFormsWithAngleSharp_ReturnsForms() {
        string html = GetSampleFormHtml();
        var forms = HtmlParser.ParseFormsWithAngleSharp(html);
        Assert.Equal(2, forms.Count);
        Assert.Equal("/login", forms[0].Metadata.Action);
        Assert.Equal("POST", forms[0].Metadata.Method);
        Assert.Equal(2, forms[0].Fields.Count);
        Assert.Equal("user", forms[0].Fields[0].Name);
    }
}
