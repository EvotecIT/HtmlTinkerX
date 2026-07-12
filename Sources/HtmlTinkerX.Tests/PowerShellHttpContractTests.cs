using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PSParseHTML.PowerShell;

namespace HtmlTinkerX.Tests;

public class PowerShellHttpContractTests {
    [Fact]
    public void InvokeHtmlCrawl_UsesCrawlerResponseLimitDefaults() {
        CmdletInvokeHtmlCrawl cmdlet = new();

        Assert.Equal(HtmlHttpFetchOptions.DefaultMaximumResponseBytes, cmdlet.MaximumPageResponseBytes);
        Assert.Equal(HtmlCrawlOptions.DefaultMaximumAssetResponseBytes, cmdlet.MaximumAssetResponseBytes);
    }

    [Fact]
    public void FindHtmlInteractionSurface_UsesBoundedResponseDefault() {
        CmdletFindHtmlInteractionSurface cmdlet = new();

        Assert.Equal(HtmlHttpFetchOptions.DefaultMaximumResponseBytes, cmdlet.MaximumResponseBytes);
    }

    [Fact]
    public void InvokeHtmlRendering_UsesBoundedLinkedScriptResponseDefault() {
        CmdletInvokeHtmlRendering cmdlet = new();

        Assert.Equal(HtmlHttpFetchOptions.DefaultMaximumResponseBytes, cmdlet.LinkedScriptMaximumResponseBytes);
    }

    [Fact]
    public void CmdletUrlReads_UseBoundedHttpUtilityOverloads() {
        string repositoryRoot = FindRepositoryRoot();
        string cmdletRoot = Path.Combine(repositoryRoot, "Sources", "PSParseHTML.PowerShell");
        List<string> unboundedCalls = Directory
            .EnumerateFiles(cmdletRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path).Select((line, index) => new { Path = path, Line = line, Number = index + 1 }))
            .Where(item =>
                (item.Line.IndexOf("HtmlUtilities.GetStringWithProperEncodingAsync(", StringComparison.Ordinal) >= 0 ||
                 item.Line.IndexOf("HtmlUtilities.ReadResponseContentWithProperEncodingAsync(", StringComparison.Ordinal) >= 0) &&
                item.Line.IndexOf("fetchOptions:", StringComparison.Ordinal) < 0)
            .Select(item => $"{Path.GetFileName(item.Path)}:{item.Number}")
            .ToList();

        Assert.True(
            unboundedCalls.Count == 0,
            $"PowerShell URL reads must use the bounded overloads.{Environment.NewLine}{string.Join(Environment.NewLine, unboundedCalls)}");
    }

    [Fact]
    public void CmdletResponseReads_StreamBeforeApplyingBodyLimits() {
        string repositoryRoot = FindRepositoryRoot();
        string cmdletRoot = Path.Combine(repositoryRoot, "Sources", "PSParseHTML.PowerShell");
        List<string> bufferedReads = Directory
            .EnumerateFiles(cmdletRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path).Select((line, index) => new { Path = path, Line = line, Number = index + 1 }))
            .Where(item => item.Line.IndexOf("GetAsync(Url", StringComparison.Ordinal) >= 0
                && item.Line.IndexOf("ResponseHeadersRead", StringComparison.Ordinal) < 0)
            .Select(item => $"{Path.GetFileName(item.Path)}:{item.Number}")
            .ToList();

        Assert.True(
            bufferedReads.Count == 0,
            $"PowerShell URL responses must stream before bounded body reads.{Environment.NewLine}{string.Join(Environment.NewLine, bufferedReads)}");
    }

    private static string FindRepositoryRoot() {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null) {
            if (File.Exists(Path.Combine(directory.FullName, "PSParseHTML.psd1"))) {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the PSParseHTML repository root.");
    }
}
