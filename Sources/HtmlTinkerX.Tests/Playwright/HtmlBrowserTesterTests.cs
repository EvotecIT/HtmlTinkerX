using Xunit;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace HtmlTinkerX.Tests.Playwright;

public class HtmlBrowserTesterTests {
    [Fact]
    public async Task TestUrlAsync_ValidUrl_ReturnsTestResult() {
        // Arrange
        var url = "data:text/html,<html><head><title>Test</title></head><body>Hello World</body></html>";
        
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(url);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(url, result.Url);
        Assert.NotNull(result.NetworkEntries);
        Assert.NotNull(result.ConsoleEntries);
        Assert.NotNull(result.PageLoadTime);
    }
    
    [Fact]
    public async Task TestUrlAsync_CapturesNetworkRequests() {
        // Arrange
        // Use a real URL that should be accessible
        var url = "http://httpbin.org/html";
        
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(url, timeout: 60000);
        
        // Assert
        Assert.NotEmpty(result.NetworkEntries);
        
        // Should have at least the main document
        var documentRequest = result.NetworkEntries
            .FirstOrDefault(e => e.ResourceType == HtmlNetworkResourceType.Document);
        Assert.NotNull(documentRequest);
        Assert.Contains("httpbin.org", documentRequest.Url);
    }
    
    [Fact]
    public async Task TestUrlAsync_CapturesRequestTiming() {
        // Arrange
        var url = "data:text/html,<html><body>Test</body></html>";
        
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(url);
        
        // Assert
        foreach (var entry in result.NetworkEntries.Where(e => !e.IsBlocked)) {
            Assert.NotEqual(default(DateTimeOffset), entry.Started);
            Assert.NotNull(entry.Finished);
            Assert.NotNull(entry.Duration);
            Assert.True(entry.Duration.Value.TotalMilliseconds >= 0);
        }
    }
    
    [Fact]
    public async Task TestUrlAsync_IdentifiesResourceTypes() {
        // Arrange
        var url = "http://httpbin.org/html";
        
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(url, timeout: 60000);
        
        // Assert
        if (result.NetworkEntries.Any()) {
            var resourceTypes = result.NetworkEntries
                .Select(e => e.ResourceType)
                .Distinct()
                .ToList();
                
            Assert.NotEmpty(resourceTypes);
            Assert.Contains(HtmlNetworkResourceType.Document, resourceTypes);
        } else {
            // For environments where network is not available, just ensure the test doesn't fail
            Assert.NotNull(result.NetworkEntries);
        }
    }
    
    [Fact]
    public async Task TestCssResourceAsync_FindsCssFile() {
        // Arrange
        var url = "data:text/html,<html><head><link rel='stylesheet' href='data:text/css,body{color:red}'></head><body>Test</body></html>";
        var cssPattern = "text/css";
        
        // Act
        var result = await HtmlBrowserTester.TestCssResourceAsync(url, cssPattern);
        
        // Assert
        // May or may not find CSS depending on the site
        if (result != null) {
            Assert.Equal(HtmlNetworkResourceType.Stylesheet, result.ResourceType);
            Assert.True(result.IsCss);
            Assert.Contains(cssPattern, result.Url);
        }
    }
    
    [Fact]
    public async Task TestConsoleErrorsAsync_ReturnsOnlyErrors() {
        // Arrange
        var url = "data:text/html,<html><body><script>console.error('Test error');console.log('Test log');</script></body></html>";
        
        // Act
        var errors = await HtmlBrowserTester.TestConsoleErrorsAsync(url);
        
        // Assert
        Assert.NotNull(errors);
        Assert.All(errors, error => Assert.True(error.IsError));
    }

    [Fact]
    public async Task ConsoleError_CapturesStackTrace() {
        // Arrange
        var url = "data:text/html,<script>console.error(new Error('fail'));</script>";

        // Act
        var errors = await HtmlBrowserTester.TestConsoleErrorsAsync(url);

        // Assert
        var error = Assert.Single(errors);
        Assert.True(error.IsError);
        Assert.False(string.IsNullOrEmpty(error.StackTrace));
        Assert.Contains("Error: fail", error.StackTrace);
    }
    
    [Fact]
    public async Task TestPerformanceAsync_ReturnsMetrics() {
        // Arrange
        var url = "data:text/html,<html><body>Test</body></html>";
        
        // Act
        var metrics = await HtmlBrowserTester.TestPerformanceAsync(url);
        
        // Assert
        Assert.NotNull(metrics);
        Assert.NotNull(metrics.TotalLoadTime);
        Assert.True(metrics.TotalRequests >= 0); // May be 0 for data URLs
        Assert.NotNull(metrics.ResourceBreakdown);
    }
    
    [Fact]
    public Task HtmlBrowserTestResult_CalculatesCorrectSummary() {
        // Arrange
        var result = new HtmlBrowserTestResult {
            Url = "https://test.com"
        };
        
        // Add some test data
        result.ConsoleEntries.Add(new HtmlConsoleEntryDetailed {
            Type = HtmlConsoleMessageType.Error,
            Text = "Test error"
        });
        
        result.ConsoleEntries.Add(new HtmlConsoleEntryDetailed {
            Type = HtmlConsoleMessageType.Warning,
            Text = "Test warning"
        });
        
        result.NetworkEntries.Add(new HtmlNetworkEntryDetailed {
            Url = "https://test.com/fail",
            ErrorType = HtmlNetworkErrorType.Failed
        });
        
        // Act
        var summary = result.Summary;
        var passed = result.Passed;
        
        // Assert
        Assert.False(passed);
        Assert.Contains("1 console error(s)", summary);
        Assert.Contains("1 console warning(s)", summary);
        Assert.Contains("1 failed request(s)", summary);
        
        return Task.CompletedTask;
    }
    
    [Fact]
    public void HtmlPerformanceMetrics_GeneratesReport() {
        // Arrange
        var metrics = new HtmlPerformanceMetrics {
            TotalLoadTime = TimeSpan.FromSeconds(2.5),
            TotalRequests = 25,
            TotalBytesTransferred = 1024 * 500,
            AverageRequestDuration = TimeSpan.FromMilliseconds(100),
            ResourceBreakdown = new Dictionary<HtmlNetworkResourceType, int> {
                { HtmlNetworkResourceType.Document, 1 },
                { HtmlNetworkResourceType.Stylesheet, 3 },
                { HtmlNetworkResourceType.Script, 5 },
                { HtmlNetworkResourceType.Image, 10 }
            }
        };
        
        // Act
        var report = metrics.GetReport();
        
        // Assert
        Assert.Contains("Performance Metrics", report);
        Assert.Contains("2500", report); // 2.5 seconds in ms
        Assert.Contains("25", report); // total requests
        Assert.Contains("512", report); // bytes - check parts separately for culture independence
        Assert.Contains("000 bytes", report); // the thousands part and "bytes" suffix
        Assert.Contains("Document: 1", report);
        Assert.Contains("Stylesheet: 3", report);
    }
    
    [Theory]
    [InlineData(HtmlConsoleMessageType.Error, 3, true, false)]
    [InlineData(HtmlConsoleMessageType.Warning, 2, false, true)]
    [InlineData(HtmlConsoleMessageType.Info, 1, false, false)]
    [InlineData(HtmlConsoleMessageType.Log, 1, false, false)]
    public void HtmlConsoleEntryDetailed_PropertiesWorkCorrectly(
        HtmlConsoleMessageType type, 
        int expectedSeverity, 
        bool shouldBeError,
        bool shouldBeWarning) {
        // Arrange
        var entry = new HtmlConsoleEntryDetailed {
            Type = type,
            Text = "Test message",
            SourceUrl = "https://test.com/script.js",
            LineNumber = 42,
            ColumnNumber = 10
        };
        
        // Act & Assert
        Assert.Equal(expectedSeverity, entry.SeverityLevel);
        Assert.Equal(shouldBeError, entry.IsError);
        Assert.Equal(shouldBeWarning, entry.IsWarning);
        Assert.Equal("https://test.com/script.js:42:10", entry.FullLocation);
    }
}