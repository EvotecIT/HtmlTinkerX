using HtmlTinkerX;
using System.IO;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlFormFieldExtractorTests {
    private static string GetSampleFormHtml() {
        string path = TestHelpers.GetDocumentPath("sample_form.html");
        return File.ReadAllText(path);
    }

    [Fact]
    public void ExtractFields_ReturnsFields() {
        string html = GetSampleFormHtml();
        var fields = HtmlFormFieldExtractor.ExtractFields(html);
        Assert.Equal(3, fields.Count);
        Assert.Equal("user", fields[0].Name);
        Assert.Equal(HtmlFormFieldType.Text, fields[0].Type);
    }

    [Fact]
    public void ExtractFields_NullHtml_Throws() {
        Assert.Throws<ArgumentNullException>(() => HtmlFormFieldExtractor.ExtractFields(null));
    }
}