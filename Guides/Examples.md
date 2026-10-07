# Parsing, formatting and browser examples

[Back to the project overview](../README.MD)

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
$session = Start-HtmlBrowserSession -Url 'https://example.com/protected' `
    -Credential $cred `
    -LoginUrl 'https://example.com/login' `
    -UsernameSelector 'input[name=username]' `
    -PasswordSelector 'input[name=password]' `
    -SubmitSelector 'button[type=submit]'

# Take screenshots with different options
Save-HtmlBrowserScreenshot -Session $session -OutFile 'full-page.png' -Full
Save-HtmlBrowserScreenshot -Session $session -OutFile 'element.png' -ElementSelector '#content'
Save-HtmlBrowserScreenshot -Session $session -OutFile 'highlighted.png' -HighlightSelector '.important'

# Download files
Save-HtmlBrowserAttachment -Session $session -Path './downloads' -Filter '.pdf'

# Network monitoring
Start-HtmlBrowserTracing -Session $session
Invoke-HtmlBrowserNavigation -Session $session -Url 'https://example.com/api/data'
Stop-HtmlBrowserTracing -Session $session -OutFile 'trace.zip'
Export-HtmlBrowserHar -Session $session -OutFile 'network.har'

Close-HtmlBrowserSession -Session $session
```

### C# Examples

#### Document Processing
```csharp
using AngleSharp.Dom;
using HtmlTinkerX;

// Parse and process HTML
string html = await File.ReadAllTextAsync("document.html");
var document = HtmlParser.ParseWithAngleSharp(html);

// Extract specific elements
var links = document.QuerySelectorAll("a[href]");
var images = document.QuerySelectorAll("img[src]");

// Extract tables with detailed information
var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(html);
foreach (var table in tables)
{
    Console.WriteLine($"Table has {table.Metadata.RowCount} rows and {table.Metadata.ColumnCount} columns");
    foreach (var row in table.Data)
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
string minifiedHtml = HtmlOptimizer.OptimizeHtml(html, cssDecodeEscapes: false);
string minifiedCss = HtmlOptimizer.OptimizeCss(css);
string minifiedJs = HtmlOptimizer.OptimizeJavaScript(javascript);

// Email optimization
string emailBody = await File.ReadAllTextAsync("newsletter.html");
var preMailerOptions = new PreMailerOptions { DownloadRemoteCss = true };
PreMailerResult preMailerResult = await PreMailerClient.MoveCssInlineAsync(emailBody, preMailerOptions);
string inlined = preMailerResult.Html;
```

#### Browser Automation
```csharp
// Basic browser session
await using var session = await HtmlBrowser.OpenSessionAsync("https://example.com");

// Authenticated session
var formLogin = new HtmlFormLogin
{
    LoginUrl = "https://example.com/login",
    UsernameSelector = "#username",
    PasswordSelector = "#password",
    SubmitSelector = "#login-button"
};
await using var authSession = await HtmlBrowser.OpenSessionAsync(
    "https://example.com/protected",
    username: "username",
    password: "password",
    formLogin: formLogin
);

// Interact with the page
await HtmlBrowser.FillInputAsync(session, "#search", "query");
await HtmlBrowser.ClickSelectorAsync(session, "#search-button");
await Task.Delay(2000); // Wait for results

// Capture results
await HtmlBrowser.CaptureScreenshotAsync(session.Page, "results.png");
var consoleMessages = HtmlBrowser.GetConsoleLog(session);
foreach (var message in consoleMessages)
{
    Console.WriteLine($"{message.Type}: {message.Text}");
}

// Download files
await foreach (string download in HtmlBrowser.SavePageDownloadsAsync(session.Page, "./downloads", ".pdf"))
{
    Console.WriteLine($"Downloaded {download}");
}
```
