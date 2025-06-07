<p align="center">
  <a href="https://dev.azure.com/evotecpl/PSParseHTML/_build/latest?definitionId=3"><img src="https://dev.azure.com/evotecpl/PSParseHTML/_apis/build/status/EvotecIT.PSParseHTML"></a>
  <a href="https://www.powershellgallery.com/packages/PSParseHTML"><img src="https://img.shields.io/powershellgallery/v/PSParseHTML.svg"></a>
  <a href="https://www.powershellgallery.com/packages/PSParseHTML"><img src="https://img.shields.io/powershellgallery/vpre/PSParseHTML.svg?label=powershell%20gallery%20preview&colorB=yellow"></a>
  <a href="https://github.com/EvotecIT/PSParseHTML"><img src="https://img.shields.io/github/license/EvotecIT/PSParseHTML.svg"></a>
</p>

<p align="center">
  <a href="https://www.powershellgallery.com/packages/PSParseHTML"><img src="https://img.shields.io/powershellgallery/p/PSParseHTML.svg"></a>
  <a href="https://github.com/EvotecIT/PSParseHTML"><img src="https://img.shields.io/github/languages/top/evotecit/PSParseHTML.svg"></a>
  <a href="https://github.com/EvotecIT/PSParseHTML"><img src="https://img.shields.io/github/languages/code-size/evotecit/PSParseHTML.svg"></a>
  <a href="https://www.powershellgallery.com/packages/PSParseHTML"><img src="https://img.shields.io/powershellgallery/dt/PSParseHTML.svg"></a>
</p>

<p align="center">
  <a href="https://twitter.com/PrzemyslawKlys"><img src="https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social"></a>
  <a href="https://evotec.xyz/hub"><img src="https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg"></a>
  <a href="https://www.linkedin.com/in/pklys"><img src="https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn"></a>
</p>

# PSParseHTML

**PSParseHTML** started as a suite of data processing Cmdlets to help [PSWriteHTML](https://github.com/EvotecIT/PSWriteHTML), but it has gained functionality enough to be its own module. Basic usage instructions are described [on this blog post](https://evotec.xyz/formatting-and-minifying-resources-html-css-javascript-with-powershell/).

**PSParseHTML** exposes a suite of PowerShell cmdlets that let you parse, format and optimise web resources right from the shell.  The module currently ships with eleven cmdlets:

- `Convert-HTMLToText` – convert markup to plain text
- `ConvertFrom-HtmlTable` – turn table elements into objects
- `ConvertFrom-HTMLAttributes` – extract elements by tag, class, id or name (aliases: `ConvertFrom-HTMLTag`, `ConvertFrom-HTMLClass`)
- `ConvertFrom-HTML` – parse full documents or fragments
- `Format-CSS` – pretty‑print style sheets
- `Format-HTML` – tidy up HTML markup
- `Format-JavaScript` – beautify JavaScript (`Format-JS` alias)
- `Optimize-CSS` – minify style sheets
- `Optimize-Email` – inline CSS for email bodies
- `Optimize-HTML` – minify HTML

- `Optimize-JavaScript` – minify JavaScript

### Cmdlet quick start

```powershell
# Convert an entire file to plain text
Convert-HTMLToText -Path '.\report.html'

# Extract all <a> tags with a specific class
ConvertFrom-HTMLAttributes -Path '.\site.html' -Class 'promo'

# Parse a snippet of markup
$doc = ConvertFrom-HTML -Content '<div>Hello</div>'

# Format a CSS style sheet
Format-CSS -Path '.\style.css'

# Beautify an HTML fragment
Format-HTML -Content $html

# Format a JavaScript file
Format-JavaScript -Path '.\script.js'

# Minify a CSS file
Optimize-CSS -Path '.\style.css'

# Inline CSS in an email body
Optimize-Email -Body $html -UseEmailFormatter

# Minify an HTML file
Optimize-HTML -Path '.\page.html'

# Minify JavaScript and save to a new file
Optimize-JavaScript -Path '.\app.js' -OutputFile '.\app.min.js'
```

The expected input is a string literal or data read from a file.  The output can be PowerShell objects (classes are `HtmlNode` or `AngleSharp.Html.Dom.HtmlElement` depending on the selected engine) or strings written to `stdout`.

It may not seem like much, but those eleven cmdlets are powerful enough to enable robust HTML processing in shell.

## Examples

```powershell
# Parse tables from a web page
$tables = ConvertFrom-HtmlTable -Url 'https://en.wikipedia.org/wiki/PowerShell'
$tables[0] | Format-Table -AutoSize

# Inline CSS in an e-mail body and pretty print the result
$html = Optimize-Email -Body $body -RemoveComments
Format-HTML -Content $html

# Minify JavaScript from a file
Optimize-JavaScript -Path './script.js' -OutputFile './script.min.js'

# Convert HTML file to plain text
Get-Content './report.html' -Raw | Convert-HTMLToText

# Extract all product entries
$markup = Get-Content './catalog.html' -Raw
ConvertFrom-HTMLAttributes -Content $markup -Class 'product'
```

## Installation

### Install from PSGallery

```powershell
Install-Module -Name PSParseHTML -AllowClobber -Force
```

Force and AllowClobber aren't necessary but they do skip errors in case some appear.

### Update from PSGallery

```powershell
Update-Module -Name PSParseHTML
```

That's it. Whenever there's a new version you simply run the `Update-Module` command and enjoy. Remember that you may need to close, re-open your **PowerShell** session if you had used the module prior to updating it.

As usual, **remember module updates may break your scripts**: if your scripts work for you in production, retain those versions until you test new versions in a dev environment. I may make small changes which are big enough so that your automated updates will break your scripts. For example, I might make a small rename to a parameter &mdash; boom, your code stops working! Be responsible!

## 3rd party references

This module utilizes several external dependencies to do its work. The authors of those libraries have done fantastic work &mdash; I've just added some PowerShell to the mix. All are distributed under permissive licenses:

- [AngleSharp](https://github.com/AngleSharp/AngleSharp) - MIT License
- [AngleSharp CSS](https://github.com/AngleSharp/AngleSharp.Css) - MIT License
- [AngleSharp.Js](https://github.com/AngleSharp/AngleSharp.Js) - MIT License
- [AngleSharp.Diffing](https://github.com/AngleSharp/Diffing) - MIT License
- [Jsbeautifier](https://github.com/denis-ivanov/Jsbeautifier) - MIT License
- [NUglify](https://github.com/trullock/NUglify) - BSD-Clause 2 license
- [Html Agility Pack](https://github.com/zzzprojects/html-agility-pack) - MIT License
- [PreMailer.Net](https://github.com/milkshakesoftware/PreMailer.Net) - Apache 2.0 License

Refer to each project's repository for complete license information.

## C# API overview

If you are writing your own .NET applications you can reference the compiled libraries directly. All classes live in the `PSParseHTML` namespace and expose methods equivalent to the cmdlets:

- `HtmlParser` – `ParseWithAngleSharp`, `ParseWithHtmlAgilityPack` and table extraction helpers such as `ParseTablesWithAngleSharpDetailed`
- `HtmlParserExtensions` – `GetElements` for quick element queries
- `HtmlFormatter` – `FormatHtml`, `FormatCss`, `FormatJavaScript`
- `HtmlOptimizer` – `OptimizeHtml`, `OptimizeCss`, `OptimizeJavaScript`
- `HtmlUtilities` – `ConvertToText` to strip markup
- `PreMailerClient` – methods like `MoveCssInline` and `MoveCssInlineFromFile`

These methods can be consumed from C# or directly from PowerShell. For example:

```csharp
string html = File.ReadAllText("example.html");
var tables = HtmlParser.ParseTablesWithHtmlAgilityPack(html);
string pretty = HtmlFormatter.FormatHtml(html);
```

```powershell
# PowerShell using the same API
[PSParseHTML.HtmlFormatter]::FormatHtml($html)
```

Both approaches yield identical results, so you can choose the most convenient tool for your workflow.
