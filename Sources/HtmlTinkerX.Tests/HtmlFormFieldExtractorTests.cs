using HtmlTinkerX;
using System.IO;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlFormFieldExtractorTests {
    private static string GetSampleFormHtml() {
        string baseDir = AppContext.BaseDirectory;
        string path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", "sample_form.html"));
        return File.ReadAllText(path);
    }

    [Fact]
    public void ExtractFields_ReturnsFields() {
        string html = GetSampleFormHtml();
        var fields = HtmlFormFieldExtractor.ExtractFields(html);
        Assert.Equal(3, fields.Count);
        Assert.Equal("user", fields[0].Name);
        Assert.Equal("text", fields[0].Type);
    }
}