using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlBrowserHarExportTests
{
    [Fact]
    public async Task ExportHarAsync_WritesEntriesToFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string file = Path.Combine(dir, "out.har");
        var network = new ConcurrentDictionary<IRequest, HtmlNetworkEntry>();
        var req1 = new Mock<IRequest>();
        network[req1.Object] = new HtmlNetworkEntry
        {
            Url = "https://example.com/1",
            Method = "GET",
            RequestHeaders = new Dictionary<string, string> { ["A"] = "1" },
            Status = 200,
            ResponseHeaders = new Dictionary<string, string> { ["B"] = "2" }
        };
        var req2 = new Mock<IRequest>();
        network[req2.Object] = new HtmlNetworkEntry
        {
            Url = "https://example.com/2",
            Method = "POST",
            RequestHeaders = new Dictionary<string, string> { ["C"] = "3" },
            Status = 201,
            ResponseHeaders = new Dictionary<string, string> { ["D"] = "4" }
        };
        var session = (HtmlBrowserSession)FormatterServices.GetUninitializedObject(typeof(HtmlBrowserSession));
        typeof(HtmlBrowserSession)
            .GetField("_network", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(session, network);

        await HtmlBrowser.ExportHarAsync(session, file);

        Assert.True(File.Exists(file));
        string json = File.ReadAllText(file);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement entries = doc.RootElement.GetProperty("log").GetProperty("entries");
        Assert.Equal(2, entries.GetArrayLength());
        var methods = new List<string>();
        var urls = new List<string>();
        var statuses = new List<int>();
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            methods.Add(entry.GetProperty("request").GetProperty("method").GetString()!);
            urls.Add(entry.GetProperty("request").GetProperty("url").GetString()!);
            statuses.Add(entry.GetProperty("response").GetProperty("status").GetInt32());
        }
        Assert.Contains("GET", methods);
        Assert.Contains("POST", methods);
        Assert.Contains("https://example.com/1", urls);
        Assert.Contains("https://example.com/2", urls);
        Assert.Contains(200, statuses);
        Assert.Contains(201, statuses);

        File.Delete(file);
        Directory.Delete(dir);
    }
}
