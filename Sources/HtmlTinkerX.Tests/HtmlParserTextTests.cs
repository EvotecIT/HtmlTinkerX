using HtmlTinkerX;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlParserTextTests {
    [Fact]
    public void ConvertToText_ReturnsExpectedText() {
        const string html = "<html><body><p>Hello <b>world</b></p></body></html>";
        string result = HtmlParserToText.ConvertToText(html).Trim();
        Assert.Equal("Hello world", result);
    }

    [Fact]
    public void StructuredTextKeepsSeparateCommandsIndentationAndSurroundingOrder() {
        const string commands = "Set-First -Enabled $true\nSet-Second -Enabled $true";
        const string registry = "[HKEY_LOCAL_MACHINE\\Software\\Example]\n    \"Enabled\"=dword:00000001";
        string html = "<p>Before commands</p><pre><code>" + commands + "</code></pre><pre>" + registry + "</pre><p>After registry</p>";

        string structured = HtmlParserToText.ConvertToText(html, preserveStructure: true).Replace("\r\n", "\n");

        Assert.Contains(commands, structured);
        Assert.Contains(registry, structured);
        Assert.Contains("Before commands", structured);
        Assert.True(structured.IndexOf("Before commands", System.StringComparison.Ordinal) < structured.IndexOf(commands, System.StringComparison.Ordinal));
        Assert.True(structured.IndexOf(commands, System.StringComparison.Ordinal) < structured.IndexOf(registry, System.StringComparison.Ordinal));
        Assert.True(structured.IndexOf(registry, System.StringComparison.Ordinal) < structured.IndexOf("After registry", System.StringComparison.Ordinal));
        string collapsed = HtmlParserToText.ConvertToText(html);
        Assert.Contains("Set-First -Enabled $true Set-Second -Enabled $true", collapsed);
        Assert.Contains("[HKEY_LOCAL_MACHINE\\Software\\Example] \"Enabled\"=dword:00000001", collapsed);
        Assert.DoesNotContain("\n", collapsed);
        Assert.Throws<System.ArgumentNullException>(() => HtmlParserToText.ConvertToText(null!, preserveStructure: true));
    }
}
