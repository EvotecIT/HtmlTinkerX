# HtmlTinkerX - Modern HTML Processing for .NET and PowerShell

HtmlTinkerX is available as NuGet from the Nuget Gallery and as PowerShell module from PSGallery

## 📦 NuGet Package
[![nuget downloads](https://img.shields.io/nuget/dt/HtmlTinkerX?label=nuget%20downloads)](https://www.nuget.org/packages/HtmlTinkerX)
[![nuget version](https://img.shields.io/nuget/v/HtmlTinkerX)](https://www.nuget.org/packages/HtmlTinkerX)

## 💻 PowerShell Module
[![powershell gallery version](https://img.shields.io/powershellgallery/v/PSParseHTML.svg)](https://www.powershellgallery.com/packages/PSParseHTML)
[![powershell gallery preview](https://img.shields.io/powershellgallery/vpre/PSParseHTML.svg?label=powershell%20gallery%20preview&colorB=yellow)](https://www.powershellgallery.com/packages/PSParseHTML)
[![powershell gallery platforms](https://img.shields.io/powershellgallery/p/PSParseHTML.svg)](https://www.powershellgallery.com/packages/PSParseHTML)
[![powershell gallery downloads](https://img.shields.io/powershellgallery/dt/PSParseHTML.svg)](https://www.powershellgallery.com/packages/PSParseHTML)

## 🛠️ Project Information
[![.NET Tests](https://github.com/EvotecIT/PSParseHTML/actions/workflows/dotnet-tests.yml/badge.svg)](https://github.com/EvotecIT/PSParseHTML/actions/workflows/dotnet-tests.yml)
[![PowerShell Tests](https://github.com/EvotecIT/PSParseHTML/actions/workflows/powershell-tests-7.yml/badge.svg)](https://github.com/EvotecIT/PSParseHTML/actions/workflows/powershell-tests-7.yml)
[![top language](https://img.shields.io/github/languages/top/evotecit/PSParseHTML.svg)](https://github.com/EvotecIT/PSParseHTML)
[![codecov](https://codecov.io/gh/EvotecIT/PSParseHTML/branch/v2-speedygonzales/graph/badge.svg)](https://codecov.io/gh/EvotecIT/PSParseHTML)

## 👨‍💻 Author & Social
[![Twitter Follow](https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social)](https://twitter.com/PrzemyslawKlys)
[![Blog](https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg)](https://evotec.xyz/hub)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn)](https://www.linkedin.com/in/pklys)
[![Discord](https://img.shields.io/discord/508328927853281280?style=flat-square&label=discord%20chat)](https://evo.yt/discord)

## What it's all about

**HtmlTinkerX** is a powerful async C# library for HTML, CSS, and JavaScript processing, parsing, formatting, and optimization. It provides comprehensive web content processing capabilities including browser automation with Playwright, document parsing with multiple engines, resource optimization, and much more. **PSParseHTML** is the PowerShell module that exposes HtmlTinkerX functionality through easy-to-use cmdlets.

Whether you're working in C# or PowerShell, you get access to:
- 🔍 **HTML Parsing** - Multiple parsing engines (AngleSharp, HtmlAgilityPack)
- 🎨 **Resource Optimization** - Minify and format HTML, CSS, JavaScript
- 🌐 **Browser Automation** - Full Playwright integration for screenshots, PDFs, interaction
- 📊 **Data Extraction** - Tables, forms, metadata, microdata, Open Graph
- 📧 **Email Processing** - CSS inlining for email compatibility
- 🔧 **Network Tools** - HAR export, request interception, console logging
- 🍪 **State Management** - Cookie handling, session persistence
- 📱 **Multi-Platform** - .NET Framework 4.7.2, .NET Standard 2.0, .NET 8.0

## 🔄 PowerShell to C# Method Mapping

This comprehensive table shows how PowerShell cmdlets map to C# methods, making it easy to transition between platforms:

### HTML Parsing & Processing
| PowerShell Cmdlet            | C# Method                                | Description                                 |
| ---------------------------- | ---------------------------------------- | ------------------------------------------- |
| `ConvertFrom-HTML`           | `HtmlParser.ParseWithAngleSharp()`       | Parse HTML documents                        |
| `ConvertFrom-HtmlTable`      | `HtmlParser.ParseTablesWithAngleSharp()` | Extract tables with rowspan/colspan support |
| `ConvertFrom-HTMLAttributes` | `HtmlParserExtensions.GetElements()`     | Query elements by tag, class, id, or name   |
| `ConvertFrom-HtmlForm`       | `HtmlFormExtractor.ExtractForms()`       | Extract form data and structure             |
| `ConvertFrom-HtmlList`       | `HtmlListParser.ParseLists()`            | Parse list elements into structured data    |
| `ConvertFrom-HtmlMeta`       | `HtmlMetaParser.ExtractMeta()`           | Extract meta tag name/content pairs         |
| `ConvertFrom-HtmlMicrodata`  | `HtmlMicrodataParser.ExtractMicrodata()` | Extract schema.org structured data          |
| `ConvertFrom-HtmlOpenGraph`  | `HtmlOpenGraphParser.ExtractOpenGraph()` | Extract Open Graph metadata                 |
| `Convert-HTMLToText`         | `HtmlUtilities.ConvertToText()`          | Convert HTML to plain text                  |
| `Compare-HTML`               | `HtmlDiffer.Compare()`                   | Compare HTML documents                      |
| `Measure-HTMLDocument`       | `HtmlParser.AnalyzeDocument()`           | Analyze document metrics                    |

### Resource Formatting & Optimization
| PowerShell Cmdlet     | C# Method                            | Description                        |
| --------------------- | ------------------------------------ | ---------------------------------- |
| `Format-HTML`         | `HtmlFormatter.FormatHtml()`         | Pretty-print HTML markup           |
| `Format-CSS`          | `HtmlFormatter.FormatCss()`          | Format CSS stylesheets             |
| `Format-JavaScript`   | `HtmlFormatter.FormatJavaScript()`   | Beautify JavaScript with options   |
| `Optimize-HTML`       | `HtmlOptimizer.OptimizeHtml()`       | Minify HTML content                |
| `Optimize-CSS`        | `HtmlOptimizer.OptimizeCss()`        | Minify CSS stylesheets             |
| `Optimize-JavaScript` | `HtmlOptimizer.OptimizeJavaScript()` | Minify JavaScript code             |
| `Optimize-Email`      | `PreMailerClient.MoveCssInline()`    | Inline CSS for email compatibility |

### Browser Automation & Sessions
| PowerShell Cmdlet       | C# Method                          | Description                      |
| ----------------------- | ---------------------------------- | -------------------------------- |
| `Start-HTMLSession`     | `HtmlBrowser.OpenSessionAsync()`   | Create browser session           |
| `Close-HTMLSession`     | `session.DisposeAsync()`           | Close browser session            |
| `Invoke-HTMLRendering`  | `HtmlBrowser.OpenSessionAsync()`   | Render pages with authentication |
| `Invoke-HTMLNavigation` | `HtmlBrowser.NavigateAsync()`      | Navigate to different URLs       |
| `Invoke-HTMLScript`     | `HtmlBrowser.ExecuteScriptAsync()` | Execute JavaScript in browser    |
| `Invoke-HTMLDomScript`  | `HtmlScriptRunner.ExecuteScript()` | Run JavaScript with AngleSharp   |
| `Export-BrowserState`   | `HtmlBrowser.ExportStateAsync()`   | Save browser state               |
| `Import-BrowserState`   | `HtmlBrowser.ImportStateAsync()`   | Restore browser state            |
| `Export-HTMLSession`    | `HtmlBrowser.ExportSessionAsync()` | Export session data              |
| `Import-HTMLSession`    | `HtmlBrowser.ImportSessionAsync()` | Import session data              |

### Screenshots & Media
| PowerShell Cmdlet          | C# Method                                | Description                     |
| -------------------------- | ---------------------------------------- | ------------------------------- |
| `Save-HTMLScreenshot`      | `HtmlBrowser.SaveScreenshotAsync()`      | Capture page screenshots        |
| `Save-HTMLPdf`             | `HtmlBrowser.SavePdfAsync()`             | Generate PDFs from pages        |
| `Start-HTMLVideoRecording` | `HtmlBrowser.StartVideoRecordingAsync()` | Start recording browser session |
| `Stop-HTMLVideoRecording`  | `HtmlBrowser.StopVideoRecordingAsync()`  | Stop video recording            |

### Element Interaction
| PowerShell Cmdlet      | C# Method                                    | Description                    |
| ---------------------- | -------------------------------------------- | ------------------------------ |
| `Invoke-HTMLClick`     | `HtmlBrowser.ClickAsync()`                   | Click elements                 |
| `Set-HTMLInput`        | `HtmlBrowser.SetInputAsync()`                | Set input field values         |
| `Set-HTMLSelectOption` | `HtmlBrowser.SetSelectAsync()`               | Select dropdown options        |
| `Set-HTMLChecked`      | `HtmlBrowser.SetCheckedAsync()`              | Check/uncheck checkboxes       |
| `Submit-HTMLForm`      | `HtmlBrowser.SubmitFormAsync()`              | Submit forms                   |
| `Get-HTMLInteractable` | `HtmlBrowser.GetInteractableElementsAsync()` | List clickable elements        |
| `Get-HTMLFormField`    | `HtmlFormExtractor.GetFormFields()`          | Extract form field information |
| `Get-HTMLLoginForm`    | `HtmlFormExtractor.DetectLoginForms()`       | Detect login forms             |

### Network & Debugging
| PowerShell Cmdlet      | C# Method                            | Description                     |
| ---------------------- | ------------------------------------ | ------------------------------- |
| `Get-HTMLNetworkLog`   | `HtmlBrowser.GetNetworkLog()`        | View network requests/responses |
| `Get-HTMLConsoleLog`   | `HtmlBrowser.GetConsoleLog()`        | Retrieve console messages       |
| `Save-HTMLHar`         | `HtmlBrowser.SaveHarAsync()`         | Export network traffic to HAR   |
| `Start-HTMLTracing`    | `HtmlBrowser.StartTracingAsync()`    | Start Playwright tracing        |
| `Stop-HTMLTracing`     | `HtmlBrowser.StopTracingAsync()`     | Stop tracing and save           |
| `Register-HTMLRoute`   | `HtmlBrowser.RegisterRouteAsync()`   | Intercept requests              |
| `Unregister-HTMLRoute` | `HtmlBrowser.UnregisterRouteAsync()` | Remove route handler            |
| `Show-HTMLHar`         | `HtmlHarViewer.ShowHar()`            | Visualize HAR files             |

### Browser Testing
| PowerShell Cmdlet       | C# Method                                        | Description                            |
| ----------------------- | ------------------------------------------------ | -------------------------------------- |
| `Test-HtmlBrowser`      | `HtmlBrowserTester.TestUrlAsync()`              | Comprehensive browser testing          |
| `Test-HtmlBrowser`      | `HtmlBrowserTester.TestFileAsync()`             | Test local HTML file (with -Path)     |
| `Test-HtmlBrowser`      | `HtmlBrowserTester.TestCssResourceAsync()`      | Test specific CSS resource (with -CssResource) |
| `Test-HtmlBrowser`      | `HtmlBrowserTester.TestConsoleErrorsAsync()`    | Get console errors (with -ErrorsOnly)  |
| `Test-HtmlBrowser`      | `HtmlBrowserTester.TestPerformanceAsync()`      | Get performance metrics (with -PerformanceOnly) |
| `Clear-HtmlBrowserCache`| `HtmlBrowserCacheCleaner.CleanAllCache()`       | Clean Playwright browser downloads     |

### Cookies & State
| PowerShell Cmdlet | C# Method                       | Description              |
| ----------------- | ------------------------------- | ------------------------ |
| `Get-HTMLCookie`  | `HtmlBrowser.GetCookiesAsync()` | Retrieve session cookies |
| `Set-HTMLCookie`  | `HtmlBrowser.SetCookieAsync()`  | Add cookies to session   |
| `New-HTMLCookie`  | `new HtmlCookie()`              | Create cookie objects    |

### Content & Resources
| PowerShell Cmdlet          | C# Method                               | Description                   |
| -------------------------- | --------------------------------------- | ----------------------------- |
| `Get-HTMLResource`         | `HtmlResourceParser.ExtractResources()` | Extract scripts and CSS       |
| `Save-HTMLAttachment`      | `HtmlBrowser.SaveAttachmentsAsync()`    | Download files from pages     |
| `Get-HTMLContent`          | `HtmlBrowser.GetContentAsync()`         | Retrieve page content         |
| `Export-HTMLOutline`       | `HtmlOutlineBuilder.BuildOutline()`     | Generate document outlines    |
| `Set-HTMLHttpClientOption` | `HttpClientFactory.ConfigureClient()`   | Configure HTTP client options |

## 📦 Installation & Packages

### 📦 NuGet Package (C#/.NET)
```bash
dotnet add package HtmlTinkerX
```

### 🔧 PowerShell Module
```powershell
Install-Module -Name PSParseHTML -AllowClobber -Force
```

### 📋 Package Information
- **📦 NuGet Package**: `HtmlTinkerX` - Core .NET library
- **🔧 PowerShell Module**: `PSParseHTML` - PowerShell cmdlets wrapper
- **🎯 Target Frameworks**: .NET Framework 4.7.2, .NET Standard 2.0, .NET 8.0
- **💻 PowerShell Compatibility**: Windows PowerShell 5.1+ and PowerShell Core 6.0+

## 🚀 Quick Start

### C# Example
```csharp
using HtmlTinkerX;

// Parse HTML and extract tables
string html = await File.ReadAllTextAsync("page.html");
var tables = HtmlParser.ParseTablesWithAngleSharp(html);

// Format and optimize resources
string formatted = HtmlFormatter.FormatHtml(html);
string minified = HtmlOptimizer.OptimizeHtml(html);

// Browser automation
await using var session = await HtmlBrowser.OpenSessionAsync("https://example.com");
await HtmlBrowser.SaveScreenshotAsync(session, "screenshot.png");
```

### PowerShell Example
```powershell
# Parse HTML tables from a webpage
$tables = ConvertFrom-HtmlTable -Url 'https://example.com'

# Format and optimize resources
$formatted = Format-HTML -Path 'page.html'
$minified = Optimize-HTML -Path 'page.html'

# Browser automation
$session = Start-HTMLSession -Url 'https://example.com'
Save-HTMLScreenshot -Session $session -OutFile 'screenshot.png'
Close-HTMLSession -Session $session
```

## 🔧 PowerShell Cmdlets

### HTML/CSS/JavaScript Processing
- **Convert-HTMLToText** - Convert markup to plain text
- **ConvertFrom-HtmlTable** - Extract table elements into objects (supports rowspan/colspan)
- **ConvertFrom-HTMLAttributes** - Extract elements by tag, class, id or name
- **ConvertFrom-HTML** - Parse full documents or fragments
- **ConvertFrom-HtmlForm** - Extract form data and structure
- **ConvertFrom-HtmlList** - Parse list elements into structured data
- **ConvertFrom-HtmlMeta** - Extract name/content pairs from meta tags
- **ConvertFrom-HtmlMicrodata** - Extract structured data items (schema.org types)
- **ConvertFrom-HtmlOpenGraph** - Extract Open Graph metadata
- **Format-CSS** - Pretty-print style sheets
- **Format-HTML** - Tidy up HTML markup
- **Format-JavaScript** - Beautify JavaScript with customizable options
- **Optimize-CSS** - Minify style sheets
- **Optimize-Email** - Inline CSS for email bodies
- **Optimize-HTML** - Minify HTML
- **Optimize-JavaScript** - Minify JavaScript

### Browser Automation & Interaction
- **Start-HTMLSession** / **Invoke-HTMLRendering** - Create browser sessions with authentication support
- **Close-HTMLSession** - Dispose browser sessions
- **Invoke-HTMLNavigation** - Navigate to different URLs
- **Invoke-HTMLScript** - Execute JavaScript in browser context
- **Invoke-HTMLDomScript** - Run JavaScript with AngleSharp (no browser required)
- **Invoke-HTMLClick** - Click elements in the browser
- **Get-HTMLInteractable** - List clickable elements
- **Set-HTMLInput** - Set input field values
- **Set-HTMLSelectOption** - Select dropdown options
- **Set-HTMLChecked** - Check/uncheck checkboxes and radio buttons
- **Submit-HTMLForm** - Submit forms

### Screenshots & Media
- **Save-HTMLScreenshot** - Capture page screenshots with advanced options
- **Save-HTMLPdf** - Generate PDFs from rendered pages
- **Start-HTMLVideoRecording** / **Stop-HTMLVideoRecording** - Record browser sessions

### Network & Debugging
- **Get-HTMLNetworkLog** - View captured network requests and responses
- **Get-HTMLConsoleLog** - Retrieve browser console messages
- **Save-HTMLHar** - Export network traffic to HAR files
- **Start-HTMLTracing** / **Stop-HTMLTracing** - Record Playwright traces
- **Register-HTMLRoute** / **Unregister-HTMLRoute** - Intercept and mock requests
- **Test-HtmlBrowser** - Comprehensive browser testing for errors, performance, and resources
- **Clear-HtmlBrowserCache** - Clean Playwright browser downloads

### Cookies & State Management
- **Get-HTMLCookie** - Retrieve cookies from sessions
- **Set-HTMLCookie** - Add cookies to sessions
- **New-HTMLCookie** - Create cookie objects
- **Export-BrowserState** / **Import-BrowserState** - Save/restore browser state
- **Export-HTMLSession** / **Import-HTMLSession** - Session state management

### Content & Resources
- **Get-HTMLResource** - Extract script and CSS resources
- **Save-HTMLAttachment** - Download files from pages
- **Get-HTMLContent** - Retrieve page content
- **Get-HTMLFormField** - Extract form field information
- **Get-HTMLLoginForm** - Detect login forms
- **Export-HTMLOutline** - Generate document outlines
- **Show-HTMLHar** - Visualize HAR files
- **Compare-HTML** - Compare HTML documents
- **Measure-HTMLDocument** - Analyze document metrics##
 🎯 C# API Reference

### Core Classes

#### HtmlParser
```csharp
// Parse with different engines
var doc = HtmlParser.ParseWithAngleSharp(html);
var doc2 = HtmlParser.ParseWithHtmlAgilityPack(html);

// Extract tables with detailed information
var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(html);
var tables2 = HtmlParser.ParseTablesWithHtmlAgilityPack(html);

// Parse from URLs
var urlDoc = await HtmlParser.ParseFromUrlAsync("https://example.com");
```

#### HtmlFormatter
```csharp
// Format different resource types
string formattedHtml = HtmlFormatter.FormatHtml(html);
string formattedCss = HtmlFormatter.FormatCss(css);
string formattedJs = HtmlFormatter.FormatJavaScript(javascript);

// Custom JavaScript formatting options
var options = new BeautifierOptions {
    IndentSize = 2,
    BraceStyle = BraceStyle.Expand
};
string customJs = HtmlFormatter.FormatJavaScript(javascript, options);

// Async operations
string formatted = await HtmlFormatter.FormatHtmlAsync(html);
```

#### HtmlOptimizer
```csharp
// Minify resources
string minifiedHtml = HtmlOptimizer.OptimizeHtml(html);
string minifiedCss = HtmlOptimizer.OptimizeCss(css);
string minifiedJs = HtmlOptimizer.OptimizeJavaScript(javascript);

// File operations
await HtmlOptimizer.OptimizeHtmlFileAsync("input.html", "output.html");
```

#### HtmlBrowser (Browser Automation)
```csharp
// Create browser sessions
await using var session = await HtmlBrowser.OpenSessionAsync("https://example.com");

// Authentication
var credentials = new NetworkCredential("user", "pass");
await using var authSession = await HtmlBrowser.OpenSessionAsync(
    "https://example.com/protected",
    credential: credentials,
    loginUrl: "https://example.com/login"
);

// Screenshots
await HtmlBrowser.SaveScreenshotAsync(session, "screenshot.png");
await HtmlBrowser.SaveScreenshotAsync(session, "full.png", fullPage: true);

// PDF generation
await HtmlBrowser.SavePdfAsync(session, "document.pdf");

// Navigation
await HtmlBrowser.NavigateAsync(session, "https://example.com/page2");

// JavaScript execution
var result = await HtmlBrowser.ExecuteScriptAsync(session, "return document.title;");

// Element interaction
await HtmlBrowser.ClickAsync(session, "#button");
await HtmlBrowser.SetInputAsync(session, "#username", "user");
await HtmlBrowser.SubmitFormAsync(session, "#loginForm");
```

#### HtmlUtilities
```csharp
// Convert HTML to plain text
string plainText = HtmlUtilities.ConvertToText(html);

// HTTP client operations
using var httpClient = HttpClientFactory.CreateHttpClient();
string content = await httpClient.GetStringAsync("https://example.com");
```

#### PreMailerClient
```csharp
// Email optimization
string inlinedCss = PreMailerClient.MoveCssInline(emailHtml);
string optimized = await PreMailerClient.MoveCssInlineAsync(emailHtml, downloadRemoteCss: true);
```

### Extension Methods

#### HtmlParserExtensions
```csharp
// Quick element queries
var elements = doc.GetElements("div.class-name");
var byId = doc.GetElements("#element-id");
var byTag = doc.GetElements("p");
```

## 📚 Examples

### PowerShell Examples

#### Table Extraction
```powershell
# Extract tables from Wikipedia
$tables = ConvertFrom-HtmlTable -Url 'https://en.wikipedia.org/wiki/PowerShell'
$tables[0] | Format-Table -AutoSize

# Parse local HTML file
$tables = ConvertFrom-HtmlTable -Path './data.html'
foreach ($table in $tables) {
    $table | Export-Csv "table_$($tables.IndexOf($table)).csv" -NoTypeInformation
}
```

#### Resource Optimization
```powershell
# Format and minify HTML
$formatted = Format-HTML -Path './messy.html'
$minified = Optimize-HTML -Content $formatted -OutputFile './clean.min.html'

# Optimize JavaScript with custom options
$js = Format-JavaScript -Path './script.js' -IndentSize 2 -BraceStyle Expand
Optimize-JavaScript -Content $js -OutputFile './script.min.js'

# Email optimization
$emailHtml = Get-Content './newsletter.html' -Raw
$optimized = Optimize-Email -Body $emailHtml -UseEmailFormatter -DownloadRemoteCss
```

#### Browser Automation
```powershell
# Authenticated session with form login
$cred = Get-Credential
$session = Start-HTMLSession -Url 'https://example.com/protected' `
    -Credential $cred `
    -LoginUrl 'https://example.com/login' `
    -UsernameSelector 'input[name=username]' `
    -PasswordSelector 'input[name=password]' `
    -SubmitSelector 'button[type=submit]'

# Take screenshots with different options
Save-HTMLScreenshot -Session $session -OutFile 'full-page.png' -Full
Save-HTMLScreenshot -Session $session -OutFile 'element.png' -ElementSelector '#content'
Save-HTMLScreenshot -Session $session -OutFile 'highlighted.png' -HighlightSelector '.important'

# Download files
Save-HTMLAttachment -Session $session -Path './downloads' -Filter '.pdf'

# Network monitoring
Start-HTMLTracing -Session $session
Invoke-HTMLNavigation -Session $session -Url 'https://example.com/api/data'
Stop-HTMLTracing -Session $session -OutFile 'trace.zip'
Save-HTMLHar -Session $session -OutFile 'network.har'

Close-HTMLSession -Session $session
```

### C# Examples

#### Document Processing
```csharp
using HtmlTinkerX;

// Parse and process HTML
string html = await File.ReadAllTextAsync("document.html");
var document = HtmlParser.ParseWithAngleSharp(html);

// Extract specific elements
var links = document.GetElements("a[href]");
var images = document.GetElements("img[src]");

// Extract tables with detailed information
var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(html);
foreach (var table in tables)
{
    Console.WriteLine($"Table has {table.Rows.Count} rows and {table.Headers.Count} columns");
    foreach (var row in table.Rows)
    {
        Console.WriteLine(string.Join(" | ", row.Values));
    }
}
```

#### Resource Optimization
```csharp
// Format resources
string formattedHtml = HtmlFormatter.FormatHtml(html);
string formattedCss = HtmlFormatter.FormatCss(css);

// Custom JavaScript formatting
var jsOptions = new BeautifierOptions
{
    IndentSize = 4,
    BraceStyle = BraceStyle.Collapse,
    PreserveNewlines = true
};
string formattedJs = HtmlFormatter.FormatJavaScript(javascript, jsOptions);

// Minification
string minifiedHtml = HtmlOptimizer.OptimizeHtml(html);
string minifiedCss = HtmlOptimizer.OptimizeCss(css);
string minifiedJs = HtmlOptimizer.OptimizeJavaScript(javascript);

// Email optimization
string emailBody = await File.ReadAllTextAsync("newsletter.html");
string inlined = await PreMailerClient.MoveCssInlineAsync(emailBody, downloadRemoteCss: true);
```

#### Browser Automation
```csharp
// Basic browser session
await using var session = await HtmlBrowser.OpenSessionAsync("https://example.com");

// Authenticated session
var credentials = new NetworkCredential("username", "password");
await using var authSession = await HtmlBrowser.OpenSessionAsync(
    "https://example.com/protected",
    credential: credentials,
    loginUrl: "https://example.com/login",
    usernameSelector: "#username",
    passwordSelector: "#password",
    submitSelector: "#login-button"
);

// Interact with the page
await HtmlBrowser.SetInputAsync(session, "#search", "query");
await HtmlBrowser.ClickAsync(session, "#search-button");
await Task.Delay(2000); // Wait for results

// Capture results
await HtmlBrowser.SaveScreenshotAsync(session, "results.png");
var consoleMessages = HtmlBrowser.GetConsoleLog(session);
foreach (var message in consoleMessages)
{
    Console.WriteLine($"{message.Type}: {message.Text}");
}

// Download files
var downloads = await HtmlBrowser.SaveAttachmentsAsync(session, "./downloads", ".pdf");
Console.WriteLine($"Downloaded {downloads.Count} files");
```

## 🧪 Browser Testing & Network Monitoring

PSParseHTML now includes comprehensive browser testing capabilities for checking network requests, CSS resources, console errors, and performance metrics. This feature uses strongly-typed classes instead of dictionaries for better IntelliSense and type safety.

### PowerShell Browser Testing

#### Basic Testing
```powershell
# Run a comprehensive test on a URL
$result = Test-HtmlBrowser -Url 'https://example.com'

# Test a local HTML file
$result = Test-HtmlBrowser -Path 'C:\MyProject\index.html'

# Check if test passed (no errors or failed requests)
if ($result.Passed) {
    Write-Host "✅ All tests passed!"
} else {
    Write-Host "❌ Issues found: $($result.Summary)"
}

# View detailed results
Write-Host "Total Requests: $($result.TotalRequests)"
Write-Host "Failed Requests: $($result.FailedRequestCount)"
Write-Host "Console Errors: $($result.ErrorCount)"
Write-Host "Console Warnings: $($result.WarningCount)"
```

#### Testing Local HTML Files
```powershell
# Test local HTML files created by HTMLForgeX or other tools
$htmlFile = "C:\Projects\MyReport\report.html"
$result = Test-HtmlBrowser -Path $htmlFile

# Check for JavaScript errors in local file
$errors = Test-HtmlBrowser -Path $htmlFile -ErrorsOnly
if ($errors.Count -gt 0) {
    Write-Host "Found $($errors.Count) JavaScript errors:"
    $errors | ForEach-Object {
        Write-Host "  - $($_.Text) at $($_.FullLocation)"
    }
}

# Test CSS loading in local file
$cssCheck = Test-HtmlBrowser -Path $htmlFile -CssResource 'styles.css'
if ($cssCheck) {
    Write-Host "CSS loaded successfully in $($cssCheck.Duration.TotalMilliseconds)ms"
}

# Test with visible browser (not headless) for debugging
$result = Test-HtmlBrowser -Path $htmlFile -Headless:$false
```

#### Testing for Console Errors
```powershell
# Get only console errors
$errors = Test-HtmlBrowser -Url 'https://example.com' -ErrorsOnly

foreach ($error in $errors) {
    Write-Host "Error: $($error.Text)"
    Write-Host "  Location: $($error.FullLocation)"
    Write-Host "  Severity: $($error.SeverityLevel)"
    
    if ($error.StackTrace) {
        Write-Host "  Stack: $($error.StackTrace)"
    }
}
```

#### CSS Resource Testing
```powershell
# Check if a specific CSS file is loaded
$cssResource = Test-HtmlBrowser -Url 'https://example.com' -CssResource 'styles.css'

if ($cssResource) {
    Write-Host "CSS found: $($cssResource.Url)"
    Write-Host "Load time: $($cssResource.Duration.TotalMilliseconds)ms"
    Write-Host "Size: $($cssResource.TransferSize) bytes"
    Write-Host "From cache: $($cssResource.ServedFromCache)"
}
```

#### Performance Testing
```powershell
# Get performance metrics only
$metrics = Test-HtmlBrowser -Url 'https://example.com' -PerformanceOnly

# Display performance report
Write-Host $metrics.GetReport()

# Access specific metrics
Write-Host "Page Load Time: $($metrics.TotalLoadTime.TotalSeconds)s"
Write-Host "Average Request Duration: $($metrics.AverageRequestDuration.TotalMilliseconds)ms"
Write-Host "Total Bytes: $($metrics.TotalBytesTransferred / 1KB)KB"

# Resource breakdown
$metrics.ResourceBreakdown | ForEach-Object {
    Write-Host "$($_.Key): $($_.Value) requests"
}
```

#### Advanced Testing with Proxy
```powershell
# Test through a proxy with authentication
$cred = Get-Credential
$result = Test-HtmlBrowser -Url 'https://example.com' `
    -Proxy 'http://proxy:8080' `
    -ProxyCredential $cred `
    -Timeout 60000
```

### C# Browser Testing

#### Basic Testing
```csharp
using HtmlTinkerX;

// Run comprehensive test on URL
var result = await HtmlBrowserTester.TestUrlAsync("https://example.com");

// Test a local HTML file
var fileResult = await HtmlBrowserTester.TestFileAsync(@"C:\MyProject\index.html");

if (result.Passed)
{
    Console.WriteLine("✅ All tests passed!");
}
else
{
    Console.WriteLine($"❌ {result.Summary}");
}

// Analyze results
Console.WriteLine($"Total Requests: {result.TotalRequests}");
Console.WriteLine($"Failed: {result.FailedRequestCount}");
Console.WriteLine($"Errors: {result.ErrorCount}");
Console.WriteLine($"Warnings: {result.WarningCount}");
```

#### Testing Local HTML Files
```csharp
// Test local HTML file with full analysis
var testResult = await HtmlBrowserTester.TestFileAsync(
    @"C:\Projects\MyReport\report.html",
    HtmlBrowserEngine.Chromium,
    headless: true,
    timeout: 30000);

// Check specific issues
if (testResult.ConsoleErrors.Any())
{
    Console.WriteLine($"Found {testResult.ErrorCount} JavaScript errors:");
    foreach (var error in testResult.ConsoleErrors)
    {
        Console.WriteLine($"  - {error.Text}");
        Console.WriteLine($"    Location: {error.FullLocation}");
        if (!string.IsNullOrEmpty(error.StackTrace))
        {
            Console.WriteLine($"    Stack: {error.StackTrace}");
        }
    }
}

// Analyze resource loading
var slowResources = testResult.NetworkEntries
    .Where(r => r.Duration > TimeSpan.FromSeconds(1))
    .OrderByDescending(r => r.Duration);

foreach (var resource in slowResources)
{
    Console.WriteLine($"Slow resource: {resource.Url} took {resource.Duration?.TotalSeconds}s");
}
```

#### Network Request Analysis
```csharp
// Test and analyze network requests
var result = await HtmlBrowserTester.TestUrlAsync("https://example.com");

// Check CSS resources
foreach (var css in result.CssResources)
{
    Console.WriteLine($"CSS: {css.Url}");
    Console.WriteLine($"  Duration: {css.Duration?.TotalMilliseconds}ms");
    Console.WriteLine($"  Size: {css.TransferSize} bytes");
    Console.WriteLine($"  Cached: {css.ServedFromCache}");
}

// Check failed requests
foreach (var failed in result.FailedRequests)
{
    Console.WriteLine($"Failed: {failed.Url}");
    Console.WriteLine($"  Error: {failed.ErrorType} - {failed.ErrorMessage}");
}

// Check JavaScript resources
var jsFiles = result.JavaScriptResources;
var totalJsSize = jsFiles.Sum(js => js.TransferSize ?? 0);
Console.WriteLine($"Total JS size: {totalJsSize / 1024}KB");
```

#### Console Error Detection
```csharp
// Get only console errors
var errors = await HtmlBrowserTester.TestConsoleErrorsAsync("https://example.com");

foreach (var error in errors)
{
    Console.WriteLine($"Error: {error.Text}");
    Console.WriteLine($"  Type: {error.Type}");
    Console.WriteLine($"  Location: {error.FullLocation}");
    Console.WriteLine($"  Timestamp: {error.Timestamp}");
    
    if (!string.IsNullOrEmpty(error.StackTrace))
    {
        Console.WriteLine($"  Stack: {error.StackTrace}");
    }
}
```

#### Performance Analysis
```csharp
// Get performance metrics
var metrics = await HtmlBrowserTester.TestPerformanceAsync("https://example.com");

// Display performance report
Console.WriteLine(metrics.GetReport());

// Check specific thresholds
if (metrics.TotalLoadTime > TimeSpan.FromSeconds(5))
{
    Console.WriteLine("⚠️ Page load time exceeds 5 seconds!");
}

if (metrics.LongestRequest?.Duration > TimeSpan.FromSeconds(2))
{
    Console.WriteLine($"⚠️ Slow resource: {metrics.LongestRequest.Url}");
}
```

### Test Result Properties

#### HtmlBrowserTestResult
- **Url** - The tested URL
- **PageLoadTime** - Total page load duration
- **NetworkEntries** - All network requests with detailed info
- **ConsoleEntries** - All console messages
- **ConsoleErrors** - Only error messages
- **ConsoleWarnings** - Only warning messages
- **FailedRequests** - Failed network requests
- **CssResources** - CSS file requests
- **JavaScriptResources** - JS file requests
- **ImageResources** - Image requests
- **Passed** - Whether all tests passed
- **Summary** - Human-readable summary

#### HtmlNetworkEntryDetailed
- **Url** - Request URL
- **Method** - HTTP method
- **Status** - Response status code
- **Duration** - Request duration
- **ResourceType** - Type of resource (Document, Stylesheet, Script, etc.)
- **TransferSize** - Total bytes transferred
- **ServedFromCache** - Whether served from cache
- **ErrorType** - Error type if failed
- **ContentType** - Response content type

#### HtmlConsoleEntryDetailed
- **Text** - Console message text
- **Type** - Message type (Error, Warning, Info, etc.)
- **Timestamp** - When logged
- **SourceUrl** - Source file URL
- **LineNumber** - Line in source
- **StackTrace** - Stack trace for errors
- **SeverityLevel** - 1=Info, 2=Warning, 3=Error
- **IsError/IsWarning/IsInfo** - Quick type checks

### Playwright Auto-Setup

Playwright browsers are automatically downloaded on first use. No manual setup required! The download happens once per system and is shared across all applications.

#### Cleaning Playwright Cache
```powershell
# View cache size and clean if needed
Clear-HtmlBrowserCache -WhatIf

# Force clean without confirmation  
Clear-HtmlBrowserCache -Force

# Skip cleaning temporary files (only clean browser downloads)
Clear-HtmlBrowserCache -SkipTemp -Force

# Skip cleaning browser downloads (only clean temp files)
Clear-HtmlBrowserCache -SkipBrowsers -Force

# View detailed information about what will be cleaned
Clear-HtmlBrowserCache -Verbose
```

The enhanced cache cleaner now:
- Cleans multiple Playwright cache locations (LocalAppData and .cache)
- Removes temporary Playwright files from the temp directory
- Cleans up trace files left behind by debugging sessions
- Shows detailed size information for each location
- Provides granular control over what to clean

#### C# Cache Cleaning
```csharp
// Get all cache locations
var locations = HtmlBrowserCacheCleaner.GetCacheLocations();
Console.WriteLine($"Found {locations.Count} locations totaling {locations.Sum(l => l.SizeMB):F2} MB");

// Clean all cache
var result = HtmlBrowserCacheCleaner.CleanAllCache();
if (result.Success)
{
    Console.WriteLine($"Cleaned {result.TotalSizeClearedMB:F2} MB");
}
else
{
    Console.WriteLine($"Failed to clean {result.Failed.Count} locations");
}

// Clean only browser downloads
var browserResult = HtmlBrowserCacheCleaner.CleanAllCache(
    includeBrowsers: true, 
    includeTemp: false);

// Get locations without cleaning (for inspection)
var tempOnly = HtmlBrowserCacheCleaner.GetCacheLocations(
    includeBrowsers: false,
    includeTemp: true);
foreach (var location in tempOnly)
{
    Console.WriteLine($"{location.Description}: {location.SizeMB:F2} MB at {location.Path}");
}
```

### Integration with Test Frameworks

#### xUnit Example
```csharp
[Fact]
public async Task WebsiteShouldHaveNoErrors()
{
    var result = await HtmlBrowserTester.TestUrlAsync("https://mysite.com");
    
    Assert.True(result.Passed, result.Summary);
    Assert.Empty(result.ConsoleErrors);
    Assert.Empty(result.FailedRequests);
}

[Fact]
public async Task CssShouldLoadQuickly()
{
    var result = await HtmlBrowserTester.TestUrlAsync("https://mysite.com");
    
    foreach (var css in result.CssResources)
    {
        Assert.True(css.Duration < TimeSpan.FromSeconds(2),
            $"CSS {css.Url} took {css.Duration?.TotalSeconds}s");
    }
}
```

#### Pester Example
```powershell
Describe "Website Health Check" {
    It "Should have no console errors" {
        $result = Test-HtmlBrowser -Url "https://mysite.com"
        $result.ErrorCount | Should -Be 0
    }
    
    It "Should load all resources successfully" {
        $result = Test-HtmlBrowser -Url "https://mysite.com"
        $result.FailedRequestCount | Should -Be 0
    }
    
    It "Should load within 5 seconds" {
        $metrics = Test-HtmlBrowser -Url "https://mysite.com" -PerformanceOnly
        $metrics.TotalLoadTime.TotalSeconds | Should -BeLessThan 5
    }
}
```

## 🔧 Advanced Features

### Browser Configuration
```powershell
# Custom browser settings
$session = Start-HTMLSession -Url 'https://example.com' `
    -UserAgent 'Custom Bot 1.0' `
    -ViewportWidth 1920 `
    -ViewportHeight 1080 `
    -DeviceScaleFactor 2 `
    -Visible `
    -SlowMo 1000
```

### Request Interception
```powershell
# Mock API responses
$handler = Register-HTMLRoute -Session $session -Pattern '**/api/data' -ScriptBlock {
    param($route)
    $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions]@{
        Status = 200
        ContentType = 'application/json'
        Body = '{"status": "success", "data": []}'
    }) | Out-Null
}

# Navigate and test
Invoke-HTMLNavigation -Session $session -Url 'https://example.com/app'
Unregister-HTMLRoute -Session $session -Pattern '**/api/data' -Handler $handler
```

### State Management
```powershell
# Save browser state
Export-BrowserState -Session $session -Path 'session-state.json'

# Restore in new session
$newSession = Import-BrowserState -Path 'session-state.json' -Url 'https://example.com/dashboard'
```

## 🏗️ Third-Party Dependencies

HtmlTinkerX utilizes several high-quality open-source libraries:

### 📦 HTML & DOM Processing
- **[AngleSharp](https://github.com/AngleSharp/AngleSharp)** - MIT License - Modern HTML5 parser
- **[AngleSharp.Css](https://github.com/AngleSharp/AngleSharp.Css)** - MIT License - CSS parsing and styling
- **[AngleSharp.Js](https://github.com/AngleSharp/AngleSharp.Js)** - MIT License - JavaScript engine integration
- **[AngleSharp.Diffing](https://github.com/AngleSharp/Diffing)** - MIT License - HTML document comparison
- **[Html Agility Pack](https://github.com/zzzprojects/html-agility-pack)** - MIT License - Alternative HTML parser

### 🎨 Resource Optimization
- **[NUglify](https://github.com/trullock/NUglify)** - BSD 2-Clause License - HTML/CSS/JS minification
- **[Jsbeautifier](https://github.com/denis-ivanov/Jsbeautifier)** - MIT License - JavaScript formatting
- **[PreMailer.Net](https://github.com/milkshakesoftware/PreMailer.Net)** - Apache 2.0 License - Email CSS inlining

### 🌐 Browser Automation
- **[Microsoft.Playwright](https://github.com/microsoft/playwright-dotnet)** - Apache 2.0 License - Browser automation
- **[SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp)** - Apache 2.0 License - Image processing
- **[SixLabors.ImageSharp.Drawing](https://github.com/SixLabors/ImageSharp.Drawing)** - Apache 2.0 License - Image drawing
- **[SixLabors.Fonts](https://github.com/SixLabors/Fonts)** - Apache 2.0 License - Font handling

### 🔧 System Libraries
- **[System.Net.Http](https://learn.microsoft.com/dotnet/api/system.net.http)** - MIT License - HTTP client
- **[System.IO.Compression](https://learn.microsoft.com/dotnet/api/system.io.compression)** - MIT License - Compression support
- **[System.Threading.Channels](https://www.nuget.org/packages/System.Threading.Channels)** - MIT License - Async communication

All dependencies are distributed under permissive licenses. Refer to each project's repository for complete license information.

## 📖 Documentation & Support

- **📚 Examples**: Check the `Examples` folder for comprehensive usage samples
- **🐛 Issues**: Report bugs and request features on [GitHub Issues](https://github.com/EvotecIT/PSParseHTML/issues)
- **💬 Discord**: Join our [Discord community](https://evo.yt/discord) for support and discussions
- **📝 Blog**: Read detailed tutorials on [evotec.xyz](https://evotec.xyz/hub)

## 🔄 Updates & Versioning

### PowerShell Module Updates
```powershell
Update-Module -Name PSParseHTML
```

### NuGet Package Updates
```bash
dotnet add package HtmlTinkerX --version latest
```

**⚠️ Important**: Always test updates in a development environment before deploying to production. Breaking changes may occur between versions.

## 📄 License

Copyright (c) 2011 - 2025 Przemyslaw Klys @ Evotec. All rights reserved.

This project and its dependencies are distributed under various permissive licenses. See individual dependency repositories for specific license terms.

---

*Built with ❤️ by [Evotec](https://evotec.xyz) - Making web content processing simple and powerful.*