using Xunit;
using HtmlTinkerX;
using System.Threading.Tasks;
using System.Linq;

namespace PSParseHTML.Examples.BrowserTesting;

/// <summary>
/// Example of using HtmlBrowserTester with xUnit test framework.
/// </summary>
public class BrowserTestingExamples {
    private const string TestUrl = "https://example.com";
    
    [Fact]
    public async Task TestPageHasNoConsoleErrors() {
        // Arrange & Act
        var result = await HtmlBrowserTester.TestUrlAsync(TestUrl);
        
        // Assert
        Assert.Empty(result.ConsoleErrors);
        Assert.True(result.Passed, $"Test failed: {result.Summary}");
    }
    
    [Fact]
    public async Task TestPageHasNoConsoleWarnings() {
        // Arrange & Act
        var result = await HtmlBrowserTester.TestUrlAsync(TestUrl);
        
        // Assert
        Assert.Empty(result.ConsoleWarnings);
    }
    
    [Fact]
    public async Task TestAllNetworkRequestsSucceed() {
        // Arrange & Act
        var result = await HtmlBrowserTester.TestUrlAsync(TestUrl);
        
        // Assert
        Assert.Empty(result.FailedRequests);
        foreach (var request in result.NetworkEntries) {
            Assert.NotNull(request.Status);
            Assert.True((int)request.Status.Value < 400, 
                $"Request failed: {request.Url} returned {request.Status}");
        }
    }
    
    [Fact]
    public async Task TestSpecificCssFileIsLoaded() {
        // Arrange
        var cssFileName = "styles.css";
        
        // Act
        var cssResource = await HtmlBrowserTester.TestCssResourceAsync(TestUrl, cssFileName);
        
        // Assert
        Assert.NotNull(cssResource);
        Assert.Contains(cssFileName, cssResource.Url);
        Assert.Equal(HtmlResourceType.Stylesheet, cssResource.ResourceType);
        Assert.NotNull(cssResource.Duration);
    }
    
    [Fact]
    public async Task TestCssLoadTime() {
        // Arrange
        var maxAcceptableTime = TimeSpan.FromSeconds(2);
        
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(TestUrl);
        var cssResources = result.CssResources.ToList();
        
        // Assert
        Assert.NotEmpty(cssResources);
        
        foreach (var css in cssResources) {
            Assert.NotNull(css.Duration);
            Assert.True(css.Duration.Value < maxAcceptableTime,
                $"CSS {css.Url} took too long to load: {css.Duration.Value.TotalMilliseconds}ms");
        }
    }
    
    [Fact]
    public async Task TestPageLoadPerformance() {
        // Arrange
        var maxLoadTime = TimeSpan.FromSeconds(5);
        var maxRequests = 50;
        
        // Act
        var metrics = await HtmlBrowserTester.TestPerformanceAsync(TestUrl);
        
        // Assert
        Assert.NotNull(metrics.TotalLoadTime);
        Assert.True(metrics.TotalLoadTime.Value < maxLoadTime,
            $"Page load took too long: {metrics.TotalLoadTime.Value.TotalSeconds}s");
        Assert.True(metrics.TotalRequests < maxRequests,
            $"Too many requests: {metrics.TotalRequests}");
    }
    
    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task TestPageInDifferentBrowsers(HtmlBrowserEngine engine) {
        // Act
        var result = await HtmlBrowserTester.TestUrlAsync(TestUrl, engine);
        
        // Assert
        Assert.True(result.Passed, $"{engine} test failed: {result.Summary}");
    }
    
    [Fact]
    public async Task TestConsoleMessageDetails() {
        // Act
        var errors = await HtmlBrowserTester.TestConsoleErrorsAsync(TestUrl);
        
        // Assert
        foreach (var error in errors) {
            // Verify we have detailed information
            Assert.NotEmpty(error.Text);
            Assert.NotNull(error.Timestamp);
            
            // Check for source information
            if (error.SourceUrl != null) {
                Assert.NotEmpty(error.SourceUrl);
                Assert.NotNull(error.LineNumber);
            }
        }
    }
}