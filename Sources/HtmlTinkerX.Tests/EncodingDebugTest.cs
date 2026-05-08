using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HtmlTinkerX.Tests;

public class EncodingDebugTest
{
    private readonly ITestOutputHelper _output;

    public EncodingDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DebugPolishEncodingIssue()
    {
        using var server = PolishEncodingFixture.CreateServer();
        using var client = PolishEncodingFixture.CreateClient(server);
        var url = "/polish";
        
        // Test with GetStringWithProperEncodingAsync
        var content = await HtmlUtilities.GetStringWithProperEncodingAsync(client, url);
        
        _output.WriteLine($"Content length: {content.Length}");
        
        // Look for Polish characters
        if (content.Contains("Komórka"))
        {
            _output.WriteLine("Polish characters detected correctly with GetStringWithProperEncodingAsync!");
        }
        else
        {
            _output.WriteLine("Polish characters NOT detected with GetStringWithProperEncodingAsync");
            
            // Find what we got instead
            var tableStart = content.IndexOf("<td>Kom");
            if (tableStart > 0)
            {
                _output.WriteLine($"Found at position {tableStart}: {content.Substring(tableStart, Math.Min(20, content.Length - tableStart))}");
            }
        }
        
        // Also test raw download to debug
        using var response = await client.GetAsync(url);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        
        _output.WriteLine($"Content-Type header: {response.Content.Headers.ContentType}");
        _output.WriteLine($"CharSet from header: {response.Content.Headers.ContentType?.CharSet}");
        
        // Test different encodings
        var encodings = new[] {
            ("UTF-8", Encoding.UTF8),
            ("ISO-8859-2", Encoding.GetEncoding("iso-8859-2")),
            ("Windows-1252", Encoding.GetEncoding(1252)),
            ("Windows-1250", Encoding.GetEncoding(1250))
        };
        
        foreach (var (name, encoding) in encodings)
        {
            var testContent = encoding.GetString(bytes);
            var hasPolish = testContent.Contains("Komórka");
            _output.WriteLine($"{name}: {(hasPolish ? "WORKS" : "FAILS")}");
            
            if (!hasPolish)
            {
                var idx = testContent.IndexOf("<td>Kom");
                if (idx > 0)
                {
                    var sample = testContent.Substring(idx + 4, Math.Min(10, testContent.Length - idx - 4));
                    _output.WriteLine($"  Sample: Kom{sample}");
                }
            }
        }
    }
}
