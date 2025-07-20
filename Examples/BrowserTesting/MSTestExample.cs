using Microsoft.VisualStudio.TestTools.UnitTesting;
using HtmlTinkerX;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace PSParseHTML.Examples.BrowserTesting;

/// <summary>
/// Example of using HtmlBrowserTester with MSTest framework.
/// </summary>
[TestClass]
public class MSTestBrowserExamples {
    private const string TestUrl = "https://example.com";
    
    [TestMethod]
    public async Task TestNoConsoleErrors() {
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(TestUrl);
        
        // Assert
        Assert.AreEqual(0, result.ErrorCount, $"Found {result.ErrorCount} console errors");
        Assert.IsTrue(result.Passed, result.Summary);
    }
    
    [TestMethod]
    public async Task TestNoNetworkFailures() {
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(TestUrl);
        
        // Assert
        Assert.AreEqual(0, result.FailedRequestCount, 
            $"Found {result.FailedRequestCount} failed requests");
            
        // Verify all status codes are successful
        foreach (var entry in result.NetworkEntries) {
            Assert.IsNotNull(entry.Status, $"No status for {entry.Url}");
            Assert.IsTrue((int)entry.Status.Value < 400, 
                $"Request {entry.Url} failed with status {entry.Status}");
        }
    }
    
    [TestMethod]
    public async Task TestCssResourceLoading() {
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(TestUrl);
        var cssResources = result.CssResources.ToList();
        
        // Assert
        CollectionAssert.AllItemsAreNotNull(cssResources);
        Assert.IsTrue(cssResources.Any(), "No CSS resources found");
        
        // Verify CSS load times
        foreach (var css in cssResources) {
            Assert.IsNotNull(css.Duration, $"No duration for {css.Url}");
            Assert.IsTrue(css.Duration.Value.TotalSeconds < 2, 
                $"CSS {css.Url} took {css.Duration.Value.TotalSeconds}s to load");
        }
    }
    
    [TestMethod]
    [DataRow(HtmlBrowserEngine.Chromium)]
    [DataRow(HtmlBrowserEngine.Firefox)]
    [DataRow(HtmlBrowserEngine.WebKit)]
    public async Task TestMultipleBrowserEngines(HtmlBrowserEngine engine) {
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(TestUrl, engine);
        
        // Assert
        Assert.IsTrue(result.Passed, $"{engine} test failed: {result.Summary}");
    }
    
    [TestMethod]
    public async Task TestPerformanceMetrics() {
        // Act
        var metrics = await HtmlBrowserTester.TestPerformanceAsync(TestUrl);
        
        // Assert
        Assert.IsNotNull(metrics.TotalLoadTime);
        Assert.IsTrue(metrics.TotalLoadTime.Value.TotalSeconds < 5, 
            $"Page load took {metrics.TotalLoadTime.Value.TotalSeconds}s");
        Assert.IsTrue(metrics.TotalRequests < 100, 
            $"Too many requests: {metrics.TotalRequests}");
            
        // Log performance report
        Console.WriteLine(metrics.GetReport());
    }
    
    [TestMethod]
    public async Task TestConsoleMessageSeverity() {
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(TestUrl);
        
        // Group messages by severity
        var errorMessages = result.ConsoleEntries.Where(e => e.IsError).ToList();
        var warningMessages = result.ConsoleEntries.Where(e => e.IsWarning).ToList();
        var infoMessages = result.ConsoleEntries.Where(e => e.IsInfo).ToList();
        
        // Assert - No errors allowed
        Assert.AreEqual(0, errorMessages.Count, 
            $"Found {errorMessages.Count} error messages");
            
        // Log summary
        Console.WriteLine($"Console Messages - Errors: {errorMessages.Count}, " +
                         $"Warnings: {warningMessages.Count}, Info: {infoMessages.Count}");
    }
    
    [TestMethod]
    public async Task TestResourceTypeBreakdown() {
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(TestUrl);
        var metrics = result.GetPerformanceMetrics();
        
        // Assert
        Assert.IsNotNull(metrics.ResourceBreakdown);
        Assert.IsTrue(metrics.ResourceBreakdown.ContainsKey(HtmlResourceType.Document),
            "No document resource found");
            
        // Log resource breakdown
        foreach (var kvp in metrics.ResourceBreakdown) {
            Console.WriteLine($"{kvp.Key}: {kvp.Value} requests");
        }
    }
    
    [TestMethod]
    public async Task TestNetworkRequestTiming() {
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(TestUrl);
        
        // Assert - All requests should have timing data
        foreach (var entry in result.NetworkEntries.Where(e => !e.IsBlocked)) {
            Assert.IsNotNull(entry.Started);
            Assert.IsNotNull(entry.Finished);
            Assert.IsNotNull(entry.Duration);
            
            // Response should come after request
            if (entry.ResponseReceived.HasValue) {
                Assert.IsTrue(entry.ResponseReceived.Value >= entry.Started,
                    $"Invalid timing for {entry.Url}");
            }
        }
    }
    
    [TestMethod]
    public async Task TestSpecificCssFile() {
        // Arrange
        var cssName = "styles.css";
        
        // Act
        var cssEntry = await HtmlBrowserTester.TestCssResourceAsync(TestUrl, cssName);
        
        // Assert
        if (cssEntry != null) {
            StringAssert.Contains(cssEntry.Url, cssName);
            Assert.AreEqual(HtmlResourceType.Stylesheet, cssEntry.ResourceType);
            Assert.IsTrue(cssEntry.IsCss);
        } else {
            Assert.Inconclusive($"CSS file '{cssName}' not found");
        }
    }
}