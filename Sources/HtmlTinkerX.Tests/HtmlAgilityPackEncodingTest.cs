using HtmlAgilityPack;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HtmlTinkerX.Tests;

public class HtmlAgilityPackEncodingTest
{
    private readonly ITestOutputHelper _output;

    public HtmlAgilityPackEncodingTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task TestHtmlAgilityPackEncodingIssue()
    {
        var url = "https://ifj.edu.pl/private/krawczyk/kurshtml/tabele/tabele.htm";
        using var client = new HttpClient();
        
        // Download with proper encoding
        var content = await HtmlUtilities.GetStringWithProperEncodingAsync(client, url);
        _output.WriteLine($"Original content contains Polish chars: {content.Contains("Komórka")}");
        
        // Parse with HtmlAgilityPack
        var doc = HtmlParser.ParseWithHtmlAgilityPack(content);
        var outerHtml = doc.DocumentNode.OuterHtml;
        
        _output.WriteLine($"After HtmlAgilityPack contains Polish chars: {outerHtml.Contains("Komórka")}");
        
        // Check for corruption
        var hasCorruption = outerHtml.Contains("�");
        _output.WriteLine($"Has corruption character: {hasCorruption}");
        
        // Find the specific table content
        var tableStart = outerHtml.IndexOf("<td>Kom");
        if (tableStart > 0)
        {
            var sample = outerHtml.Substring(tableStart, System.Math.Min(30, outerHtml.Length - tableStart));
            _output.WriteLine($"Table content: {sample}");
            
            // Check bytes
            var bytes = System.Text.Encoding.UTF8.GetBytes(sample);
            _output.WriteLine($"UTF-8 bytes: {string.Join(" ", bytes)}");
        }
        
        // Try different encoding for HtmlAgilityPack
        _output.WriteLine($"\nHtmlAgilityPack default encoding: {doc.Encoding?.EncodingName ?? "null"}");
        
        // Test setting encoding explicitly
        var doc2 = new HtmlDocument();
        doc2.OptionDefaultStreamEncoding = System.Text.Encoding.GetEncoding("iso-8859-2");
        doc2.LoadHtml(content);
        var outerHtml2 = doc2.DocumentNode.OuterHtml;
        _output.WriteLine($"With explicit encoding contains Polish chars: {outerHtml2.Contains("Komórka")}");
        _output.WriteLine($"With explicit encoding has corruption: {outerHtml2.Contains("�")}");
    }
}