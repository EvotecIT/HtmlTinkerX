# HtmlTinkerX & PSParseHTML - HTML processing for .NET and PowerShell

HtmlTinkerX is the shared .NET engine for parsing, extracting, auditing,
formatting, crawling, and rendering web content. PSParseHTML exposes the same
engine as PowerShell cmdlets.

## 📦 NuGet Package
[![nuget downloads](https://img.shields.io/nuget/dt/HtmlTinkerX?label=nuget%20downloads)](https://www.nuget.org/packages/HtmlTinkerX)
[![nuget version](https://img.shields.io/nuget/v/HtmlTinkerX)](https://www.nuget.org/packages/HtmlTinkerX)

## 💻 PowerShell Module
[![powershell gallery version](https://img.shields.io/powershellgallery/v/PSParseHTML.svg)](https://www.powershellgallery.com/packages/PSParseHTML)
[![powershell gallery platforms](https://img.shields.io/powershellgallery/p/PSParseHTML.svg)](https://www.powershellgallery.com/packages/PSParseHTML)
[![powershell gallery downloads](https://img.shields.io/powershellgallery/dt/PSParseHTML.svg)](https://www.powershellgallery.com/packages/PSParseHTML)

## 🛠️ Project Information
[![.NET Tests](https://github.com/EvotecIT/HtmlTinkerX/actions/workflows/dotnet-tests.yml/badge.svg)](https://github.com/EvotecIT/HtmlTinkerX/actions/workflows/dotnet-tests.yml)
[![PowerShell Tests](https://github.com/EvotecIT/HtmlTinkerX/actions/workflows/powershell-tests-7.yml/badge.svg)](https://github.com/EvotecIT/HtmlTinkerX/actions/workflows/powershell-tests-7.yml)
[![top language](https://img.shields.io/github/languages/top/evotecit/HtmlTinkerX.svg)](https://github.com/EvotecIT/HtmlTinkerX)
[![codecov](https://codecov.io/gh/EvotecIT/HtmlTinkerX/branch/v2-speedygonzales/graph/badge.svg)](https://codecov.io/gh/EvotecIT/HtmlTinkerX)

Dependency guardrails, including the ChartForgeX-backed screenshot image-processing path, are documented in [Docs/Dependencies.md](Docs/Dependencies.md).

## 👨‍💻 Author & Social
[![Twitter Follow](https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social)](https://twitter.com/PrzemyslawKlys)
[![Blog](https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg)](https://evotec.xyz/hub)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn)](https://www.linkedin.com/in/pklys)
[![Discord](https://img.shields.io/discord/508328927853281280?style=flat-square&label=discord%20chat)](https://evo.yt/discord)

## What it covers

- HTML parsing with AngleSharp and Html Agility Pack
- object-first page reading with headings, paragraphs, tables, links, resources,
  and inferred repeated collections
- tables, lists, forms, metadata, JSON-LD, microdata, Open Graph, application
  state, tokens, image candidates, and API endpoint extraction
- static and rendered document audits for duplicate IDs, document metadata,
  accessible names, unsafe URL schemes, and heading order
- bounded website crawling to offline HTML, text, Markdown, JSON, JSONL, graph,
  and asset datasets
- Playwright sessions, interaction, screenshots, PDFs, HAR files, traces,
  browser recipes, cookies, storage, and SSO handoff inspection
- HTML, CSS, JavaScript, and email formatting or optimization
- .NET Framework 4.7.2, .NET 8, and .NET 10

## 📦 Installation & Packages

### 📦 NuGet Package (C#/.NET)
```bash
dotnet add package HtmlTinkerX
```

### 🔧 PowerShell Module
```powershell
Install-Module -Name PSParseHTML -AllowClobber -Force
```

These commands install the current stable releases. Upstream dependency version
labels do not change HtmlTinkerX or PSParseHTML release channels.

### 📋 Package Information
- **📦 NuGet Package**: `HtmlTinkerX` - Core .NET library
- **🔧 PowerShell Module**: `PSParseHTML` - PowerShell cmdlets wrapper
- **🎯 Target Frameworks**: .NET Framework 4.7.2, .NET 8.0, and .NET 10.0
- **💻 PowerShell Compatibility**: Windows PowerShell 5.1 and PowerShell 7.4+

## 🚀 Quick Start

### Read a page as objects

Start with a local HTML string so you can inspect the result immediately:

```powershell
$html = @'
<!doctype html>
<html><head><title>Products</title></head><body>
<main>
  <h1>Products</h1>
  <p>Two items are available.</p>
  <table><tr><th>Name</th><th>Price</th></tr>
    <tr><td>Desk</td><td>120</td></tr>
    <tr><td>Chair</td><td>40</td></tr>
  </table>
</main>
</body></html>
'@
$page = Get-HtmlPage -Content $html
$page.Headings
$page.Paragraphs
$page.Tables
```

For a live page, use `Get-HtmlPage -Url $url`. The result also exposes links,
forms, resources, inferred repeated collections, readable text and Markdown.
The [object workflow guide](Docs/Examples/07-GetHtmlPageObjectWorkflows/README.MD)
shows how to inspect tables, choose collections by their fields and reuse
selectors when you need a stable extraction recipe.

Skip analyses you do not need:

```powershell
$page = Get-HtmlPage -Content $html -NoReadableText -NoMarkdown -NoWebData -NoCollections
$page.Headings
$page.Tables
```

For client-rendered pages, read a browser snapshot:

```powershell
$snapshot = Invoke-HtmlRendering -Url $url -Snapshot
$page = Get-HtmlPage -RenderedSnapshot $snapshot
```

### Read a page in C#

```csharp
using System;
using HtmlTinkerX;

HtmlPageDocument page = HtmlPageReader.Read(
    "<html><head><title>Products</title></head><body>"
    + "<main><h1>Products</h1><p>Two items are available.</p></main></body></html>");
Console.WriteLine(page.Headings[0].Text);
```

- <a id="c-example"></a>[C# Example](Guides/DotNet.md#c-example)
- <a id="powershell-example"></a>[PowerShell Example](Guides/Examples.md#powershell-examples)

## Workflow guides

| Workflow | Guide |
| --- | --- |
| Read semantic objects and inferred collections | [Object workflows](Docs/Examples/07-GetHtmlPageObjectWorkflows/README.MD) |
| Crawl pages, mirror assets, choose content and resume a dataset | [Crawling and exports](Guides/Crawling.md) |
| Parse and audit documents or automate a browser in PowerShell | [PowerShell workflows](Guides/PowerShell.md) |
| Call the .NET engine and inspect its public APIs | [.NET examples and API overview](Guides/DotNet.md) |
| Parse local files, format resources and capture browser output | [More examples](Guides/Examples.md) |
| Check page errors, network requests, media and browser installation | [Browser testing](Guides/BrowserTesting.md) |
| Configure request interception, profiles and saved state | [Browser sessions](Guides/BrowserSessions.md) |
| Set HTTP response limits and understand encoding or cancellation | [HTTP response controls](Guides/HttpResponses.md) |

The [generated command reference](Docs/Readme.md) contains the complete
PowerShell parameter and pipeline documentation. See [MIGRATION.md](MIGRATION.md)
for API changes when upgrading.

## HTTP response controls

URL parsing, HTTP form submission and related shared readers enforce a response
size limit and honor cancellation. See [HTTP response controls](Guides/HttpResponses.md)
for the defaults, explicit overrides and decoding order.

## 🔧 PowerShell Cmdlets

Use the [command reference](Docs/Readme.md) for individual commands and the
[PowerShell workflow guide](Guides/PowerShell.md) for combinations of commands.

- <a id="htmlcssjavascript-processing"></a>[HTML/CSS/JavaScript Processing](Guides/PowerShell.md#htmlcssjavascript-processing)
- <a id="browser-automation--interaction"></a>[Browser Automation & Interaction](Guides/PowerShell.md#browser-automation--interaction)
- <a id="browser-extraction-mode"></a>[Browser Extraction Mode](Guides/PowerShell.md#browser-extraction-mode)
- <a id="browserless-extraction-mode"></a>[Browserless Extraction Mode](Guides/PowerShell.md#browserless-extraction-mode)
- <a id="screenshots--media"></a>[Screenshots & Media](Guides/PowerShell.md#screenshots--media)
- <a id="network--debugging"></a>[Network & Debugging](Guides/PowerShell.md#network--debugging)
- <a id="cookies--state-management"></a>[Cookies & State Management](Guides/PowerShell.md#cookies--state-management)
- <a id="content--resources"></a>[Content & Resources](Guides/PowerShell.md#content--resources)
- <a id="javascript-ast-parsing"></a>[JavaScript AST parsing](Guides/PowerShell.md#javascript-ast-parsing)
- <a id="css-and-html-workflow-audits"></a>[CSS and HTML workflow audits](Guides/PowerShell.md#css-and-html-workflow-audits)
- <a id="react-server-component-payload-extraction"></a>[React Server Component payload extraction](Guides/PowerShell.md#react-server-component-payload-extraction)
- <a id="modern-page-parsing-helpers"></a>[Modern page parsing helpers](Guides/PowerShell.md#modern-page-parsing-helpers)

## 🎯 C# API Reference

The [.NET guide](Guides/DotNet.md) covers the parsing, extraction, crawl and
browser APIs with examples.

- <a id="core-classes"></a>[Core Classes](Guides/DotNet.md#core-classes)
- <a id="htmlparser"></a>[HtmlParser](Guides/DotNet.md#htmlparser)
- <a id="htmlformatter"></a>[HtmlFormatter](Guides/DotNet.md#htmlformatter)
- <a id="htmloptimizer"></a>[HtmlOptimizer](Guides/DotNet.md#htmloptimizer)
- <a id="htmlbrowser-browser-automation"></a>[HtmlBrowser (Browser Automation)](Guides/DotNet.md#htmlbrowser-browser-automation)
- <a id="htmlutilities"></a>[HtmlUtilities](Guides/DotNet.md#htmlutilities)
- <a id="premailerclient"></a>[PreMailerClient](Guides/DotNet.md#premailerclient)
- <a id="extension-methods"></a>[Extension Methods](Guides/DotNet.md#extension-methods)
- <a id="htmlparserextensions"></a>[HtmlParserExtensions](Guides/DotNet.md#htmlparserextensions)

## 📚 Examples

See [workflow examples](Guides/Examples.md), [PowerShell scripts](Examples/)
and the [.NET example project](Sources/HtmlTinkerX.Examples/).

- <a id="powershell-examples"></a>[PowerShell Examples](Guides/Examples.md#powershell-examples)
- <a id="table-extraction"></a>[Table Extraction](Guides/Examples.md#table-extraction)
- <a id="resource-optimization"></a>[Resource Optimization](Guides/Examples.md#resource-optimization)
- <a id="browser-automation"></a>[Browser Automation](Guides/Examples.md#browser-automation)
- <a id="c-examples"></a>[C# Examples](Guides/Examples.md#c-examples)
- <a id="document-processing"></a>[Document Processing](Guides/Examples.md#document-processing)
- <a id="resource-optimization-1"></a>[Resource Optimization](Guides/Examples.md#resource-optimization-1)
- <a id="browser-automation-1"></a>[Browser Automation](Guides/Examples.md#browser-automation-1)

## 🧪 Browser Testing & Network Monitoring

The [browser testing guide](Guides/BrowserTesting.md) covers page checks,
console and network logs, performance metrics, browser setup and troubleshooting.

- <a id="powershell-browser-testing"></a>[PowerShell Browser Testing](Guides/BrowserTesting.md#powershell-browser-testing)
- <a id="basic-testing"></a>[Basic Testing](Guides/BrowserTesting.md#basic-testing)
- <a id="testing-local-html-files"></a>[Testing Local HTML Files](Guides/BrowserTesting.md#testing-local-html-files)
- <a id="testing-for-console-errors"></a>[Testing for Console Errors](Guides/BrowserTesting.md#testing-for-console-errors)
- <a id="css-resource-testing"></a>[CSS Resource Testing](Guides/BrowserTesting.md#css-resource-testing)
- <a id="performance-testing"></a>[Performance Testing](Guides/BrowserTesting.md#performance-testing)
- <a id="advanced-testing-with-proxy"></a>[Advanced Testing with Proxy](Guides/BrowserTesting.md#advanced-testing-with-proxy)
- <a id="batch-testing-multiple-urls"></a>[Batch Testing Multiple URLs](Guides/BrowserTesting.md#batch-testing-multiple-urls)
- <a id="integration-with-pester-tests"></a>[Integration with Pester Tests](Guides/BrowserTesting.md#integration-with-pester-tests)
- <a id="testing-local-html-reports"></a>[Testing Local HTML Reports](Guides/BrowserTesting.md#testing-local-html-reports)
- <a id="monitoring-and-alerting"></a>[Monitoring and Alerting](Guides/BrowserTesting.md#monitoring-and-alerting)
- <a id="c-browser-testing"></a>[C# Browser Testing](Guides/BrowserTesting.md#c-browser-testing)
- <a id="basic-testing-1"></a>[Basic Testing](Guides/BrowserTesting.md#basic-testing-1)
- <a id="testing-local-html-files-1"></a>[Testing Local HTML Files](Guides/BrowserTesting.md#testing-local-html-files-1)
- <a id="network-request-analysis"></a>[Network Request Analysis](Guides/BrowserTesting.md#network-request-analysis)
- <a id="console-error-detection"></a>[Console Error Detection](Guides/BrowserTesting.md#console-error-detection)
- <a id="performance-analysis"></a>[Performance Analysis](Guides/BrowserTesting.md#performance-analysis)
- <a id="testing-local-html-files-2"></a>[Testing Local HTML Files](Guides/BrowserTesting.md#testing-local-html-files-2)
- <a id="integration-testing-examples"></a>[Integration Testing Examples](Guides/BrowserTesting.md#integration-testing-examples)
- <a id="batch-testing-multiple-pages"></a>[Batch Testing Multiple Pages](Guides/BrowserTesting.md#batch-testing-multiple-pages)
- <a id="test-result-properties"></a>[Test Result Properties](Guides/BrowserTesting.md#test-result-properties)
- <a id="htmlbrowsertestresult"></a>[HtmlBrowserTestResult](Guides/BrowserTesting.md#htmlbrowsertestresult)
- <a id="htmlnetworkentrydetailed"></a>[HtmlNetworkEntryDetailed](Guides/BrowserTesting.md#htmlnetworkentrydetailed)
- <a id="htmlconsoleentrydetailed"></a>[HtmlConsoleEntryDetailed](Guides/BrowserTesting.md#htmlconsoleentrydetailed)
- <a id="htmlperformancemetrics"></a>[HtmlPerformanceMetrics](Guides/BrowserTesting.md#htmlperformancemetrics)
- <a id="playwright-auto-setup"></a>[Playwright Auto-Setup](Guides/BrowserTesting.md#playwright-auto-setup)
- <a id="how-auto-download-works"></a>[How Auto-Download Works](Guides/BrowserTesting.md#how-auto-download-works)
- <a id="linux-avoiding-sudo-prompts"></a>[Linux: Avoiding sudo prompts](Guides/BrowserTesting.md#linux-avoiding-sudo-prompts)
- <a id="cleaning-playwright-cache"></a>[Cleaning Playwright Cache](Guides/BrowserTesting.md#cleaning-playwright-cache)
- <a id="c-cache-cleaning"></a>[C# Cache Cleaning](Guides/BrowserTesting.md#c-cache-cleaning)
- <a id="integration-with-test-frameworks"></a>[Integration with Test Frameworks](Guides/BrowserTesting.md#integration-with-test-frameworks)
- <a id="xunit-example"></a>[xUnit Example](Guides/BrowserTesting.md#xunit-example)
- <a id="pester-example"></a>[Pester Example](Guides/BrowserTesting.md#pester-example)

## 🔧 Advanced Features

The [browser session guide](Guides/BrowserSessions.md) covers context settings,
request interception and saved state.

- <a id="browser-configuration"></a>[Browser Configuration](Guides/BrowserSessions.md#browser-configuration)
- <a id="request-interception"></a>[Request Interception](Guides/BrowserSessions.md#request-interception)
- <a id="state-management"></a>[State Management](Guides/BrowserSessions.md#state-management)

## Form fields and submission values

`ConvertFrom-HtmlForm` returns `Fields` as the inventory of named controls
associated with each form, including disabled controls and controls linked by
a `form` attribute. `SuccessfulFields` contains the ordered name/value pairs
that are eligible for a submission without a submit button. Repeated names
remain separate, and radio and select values use the document's current
selection. File inputs and submit buttons are omitted from this value list.

Supply `-BaseUri` when parsing HTML content to resolve a relative action:

```powershell
$html = '<form action="save"><input name="tag" value="one"><input name="tag" value="two"></form>'
$form = ConvertFrom-HtmlForm -Content $html -BaseUri 'https://example.com/settings/' -IncludeMetadata
$form.ResolvedAction       # https://example.com/settings/save
$form.SuccessfulFields     # tag=one and tag=two as separate Key/Value pairs
```

`Action` preserves the original attribute. With `-IncludeMetadata`,
`SourceUrl`, `FinalUrl`, and `BaseUrl` identify the requested document,
redirect destination, and effective HTML base address. Non-HTTP actions
remain visible in `Action` and have no resolved HTTP submission address.

.NET callers use the same engine:

```csharp
var forms = HtmlParser.ParseFormsWithAngleSharp(html, new Uri("https://example.com/settings/"));
var action = forms[0].Metadata.ResolvedActionUri;
var values = forms[0].SuccessfulFields;
```

## PowerShell and C# entry points

The PowerShell commands are thin surfaces over HtmlTinkerX. The generated
[command reference](Docs/Readme.md) covers every cmdlet.

| Task | PowerShell | C# owner |
| --- | --- | --- |
| Read a page as objects without selectors | `Get-HtmlPage` | `HtmlPageReader.Read` |
| Parse a document | `ConvertFrom-Html` | `HtmlParser.ParseWithAngleSharp`, `HtmlParser.ParseWithHtmlAgilityPack` |
| Extract tables | `ConvertFrom-HtmlTable` | `HtmlParser.ParseTablesWithAngleSharpDetailed`, `HtmlParser.ParseTablesWithHtmlAgilityPackDetailed` |
| Extract lists | `ConvertFrom-HtmlList` | `HtmlParser.ParseListsWithAngleSharpDetailed`, `HtmlParser.ParseListsWithHtmlAgilityPackDetailed` |
| Extract forms | `ConvertFrom-HtmlForm` | `HtmlParser.ParseFormsWithAngleSharp` |
| Extract metadata | `ConvertFrom-HtmlMeta` | `HtmlParser.ParseMetaTags` |
| Extract Open Graph data | `ConvertFrom-HtmlOpenGraph` | `HtmlParser.ParseOpenGraph` |
| Extract microdata | `ConvertFrom-HtmlMicrodata` | `HtmlParser.ParseMicrodataItems` |
| Normalize mixed page data | `Select-HtmlData` | `HtmlParsingToolbox.SelectData` |
| Build a page workbench and audit | `Invoke-HtmlPageWorkbench` | `HtmlPageWorkbench.AnalyzeAsync`, `HtmlDocumentAudit.Analyze` |
| Find interaction surfaces | `Find-HtmlInteractionSurface` | `HtmlParsingToolbox.FindInteractionSurfaceAsync` |
| Discover API endpoints | `Find-HtmlApiEndpoint` | `HtmlApiEndpointInventory.Build` |
| Compare static and rendered HTML | `Compare-HtmlStaticRendered` | `HtmlParsingToolbox.CompareStaticRendered` |
| Crawl and export a dataset | `Invoke-HtmlCrawl` | `HtmlCrawler.CrawlAsync` |
| Open or navigate a browser | `Start-HtmlBrowserSession`, `Invoke-HtmlBrowserNavigation` | `HtmlBrowser.OpenSessionAsync`, `HtmlBrowser.NavigateAsync` |
| Click or fill an element | `Invoke-HtmlBrowserClick`, `Set-HtmlBrowserInput` | `HtmlBrowser.ClickSelectorAsync`, `HtmlBrowser.FillInputAsync` |
| Capture a screenshot or PDF | `Save-HtmlBrowserScreenshot`, `Save-HtmlBrowserPdf` | `HtmlBrowser.CaptureScreenshotAsync`, `HtmlBrowser.SavePagePdfAsync` |
| Export HAR or trace data | `Export-HtmlBrowserHar`, `Start-HtmlBrowserTracing`, `Stop-HtmlBrowserTracing` | `HtmlBrowser.ExportHarAsync`, `HtmlBrowser.StartTracingAsync`, `HtmlBrowser.StopTracingAsync` |
| Test a rendered page | `Test-HtmlBrowser` | `HtmlBrowserTester` |
| Inline email CSS | `Optimize-Email` | `PreMailerClient.MoveCssInline`, `PreMailerClient.MoveCssInlineAsync` |
| Format or minify resources | `Format-Html`, `Format-Css`, `Format-JavaScript`, `Optimize-*` | `HtmlFormatter`, `HtmlOptimizer` |

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
- **[ChartForgeX](https://github.com/EvotecIT/ChartForgeX)** - MIT License - Dependency-free screenshot image composition

Screenshot image post-processing is routed through ChartForgeX. See [Docs/Dependencies.md](Docs/Dependencies.md) before changing that dependency path.

### 🔧 System Libraries
- **[System.Net.Http](https://learn.microsoft.com/dotnet/api/system.net.http)** - MIT License - HTTP client
- **[System.IO.Compression](https://learn.microsoft.com/dotnet/api/system.io.compression)** - MIT License - Compression support
- **[System.Threading.Channels](https://www.nuget.org/packages/System.Threading.Channels)** - MIT License - Async communication

All dependencies are distributed under permissive licenses. Refer to each project's repository for complete license information.

## 📖 Documentation & Support

- **📚 Examples**: Check the `Examples` folder for comprehensive usage samples
- **🐛 Issues**: Report bugs and request features on [GitHub Issues](https://github.com/EvotecIT/HtmlTinkerX/issues)
- **💬 Discord**: Join our [Discord community](https://evo.yt/discord) for support and discussions
- **📝 Blog**: Read detailed tutorials on [evotec.xyz](https://evotec.xyz/hub)

## 🔄 Updates & Versioning

### PowerShell Module Updates
```powershell
Update-Module -Name PSParseHTML
```

### NuGet Package Updates
```bash
dotnet add package HtmlTinkerX
```

**⚠️ Important**: Always test updates in a development environment before deploying to production. Breaking changes may occur between versions.

## 🔧 Troubleshooting

- <a id="browser-extraction-mode-1"></a>[Browser Extraction Mode](Guides/BrowserTesting.md#browser-extraction-mode)
- <a id="jint-and-javascript-parser-notes"></a>[Jint and JavaScript Parser Notes](Guides/BrowserTesting.md#jint-and-javascript-parser-notes)
- <a id="browser-testing-issues"></a>[Browser Testing Issues](Guides/BrowserTesting.md#browser-testing-issues)

## 📄 License

HtmlTinkerX and PSParseHTML are available under the [MIT License](LICENSE).
Third-party components remain under their own license terms.
