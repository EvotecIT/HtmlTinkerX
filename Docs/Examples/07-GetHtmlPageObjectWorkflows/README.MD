# Read web pages as PowerShell objects

`Get-HtmlPage` is the starting point when you know what information you want
from a page but do not know its CSS selectors.

It accepts one page input and returns several views of the same content:

- `Headings`, `Paragraphs`, `Lists`, and `Tables` for document content
- `Links`, `Forms`, `Resources`, and `Assets` for web-specific objects
- `Collections` for repeated cards, rows, search results, or listings
- `Markdown` and `ReadableText` for text-oriented tools
- `SemanticDocument` and `LogicalDocument` for the complete OfficeIMO.Html
  object model

The objects retain source locations and selectors as provenance. You can use
them for troubleshooting, but you do not have to supply a selector to begin.

For parameter details, see the generated
[`Get-HtmlPage` command reference](../../Get-HtmlPage.md).

## Start by looking at the page summary

```powershell
$page = Get-HtmlPage -Url 'https://books.toscrape.com/'

$page | Format-List `
    Title,
    FinalUrl,
    AnalysisMode,
    HeadingCount,
    ParagraphCount,
    TableCount,
    CollectionCount
```

Representative output:

```text
Title           : All products | Books to Scrape - Sandbox
FinalUrl        : https://books.toscrape.com/
AnalysisMode    : Static
HeadingCount    : 1
ParagraphCount  : 2
TableCount      : 0
CollectionCount : 3
```

The counts tell you which view is likely to be useful. A documentation page
usually leads to `Headings` and `Paragraphs`; a catalog often leads to
`Collections`; a report may lead to `Tables`.

## Read an article or documentation page

Headings and paragraphs are typed semantic blocks. They include their text,
heading level, inline runs, computed style, child blocks, and source location.

```powershell
$page.Headings |
    Select-Object Level, Text, SourceLocation

$page.Paragraphs |
    Select-Object Text, SourceLocation
```

Use `Sections` when hierarchy matters and `Blocks` when you want every retained
semantic block in document order:

```powershell
$page.Sections

$page.Blocks |
    Select-Object Kind, Level, Text, SourceLocation
```

Use Markdown when the next tool wants text rather than page objects:

```powershell
$page.Markdown | Set-Content -Path './page.md'
```

Markdown is a projection, not the extraction source. Keep `$page` when you may
still need tables, links, resources, source locations, or repeated records.

## Work with a typed table

`Tables` preserves captions, rows, cells, headers, spans, rich text runs, and
source locations.

```powershell
$table = $page.Tables | Select-Object -First 1

$table.Caption
$table.Rows.Count

$table.Rows | ForEach-Object {
    [pscustomobject]@{
        Cells = ($_.Cells.Text -join ' | ')
    }
}
```

A cell exposes more than its displayed text:

```powershell
$cell = $table.Rows[0].Cells[0]

$cell | Format-List `
    Text,
    IsHeader,
    RowSpan,
    ColumnSpan,
    Runs,
    Resources,
    SourceLocation
```

If you only need a flat PowerShell table and do not need the rest of the page,
`ConvertFrom-HtmlTable` remains the shorter path:

```powershell
ConvertFrom-HtmlTable -Url 'https://example.org/report' |
    Format-Table
```

## Find repeated objects without knowing selectors

Pages often repeat the same HTML shape for products, search results, news
stories, people, or events. `Collections` ranks those repeated structures and
extracts their fields.

Start by inspecting the candidates:

```powershell
$page.Collections |
    Select-Object Name, Count, Confidence, Score,
        @{ Name = 'Fields'; Expression = { $_.Fields.Name -join ', ' } } |
    Format-Table -AutoSize
```

Representative output from Books To Scrape:

```text
Name           Count Confidence Fields
----           ----- ---------- ------
Product Pods      20 High       Link, Price, Image, ImageLink, Btn, LinkText, Title, Instock
Product Prices    20 Medium     Price, Btn, Instock
List Items        75 Low        Link, LinkText
```

Do not assume that the first collection or a particular field name will exist
on every website. Select the candidate by the fields relevant to your task:

```powershell
$products = $page.Collections |
    Where-Object {
        $_.Fields.Name -contains 'Title' -and
        $_.Fields.Name -contains 'Price'
    } |
    Sort-Object Score -Descending |
    Select-Object -First 1

$products.Fields |
    Select-Object Name, Attribute, Selector, MultiplePerItem

$products.Items |
    Select-Object Title, Price, Link, Image
```

Each item is an ordinary PowerShell object. Its discovered fields are direct
properties, while `Values` provides the same data as a dictionary:

```powershell
$item = $products.Items[0]

$item.Title
$item.Price
$item.Values['Title']
$item.PSObject.Properties.Name
```

Field names come from the page structure. One site may expose `Link`; another
may expose `ProductLink`. Inspect `Fields` or the first item's properties before
building a long-lived export.

For a CSV export:

```powershell
$products.Items |
    Select-Object Title, Price, Link, Image |
    Export-Csv -Path './products.csv' -NoTypeInformation
```

## Focus collection discovery

Use a hint when the page contains many unrelated repeated structures:

```powershell
$page = Get-HtmlPage `
    -Url 'https://example.org/catalog' `
    -CollectionHint 'product'
```

You can also change the minimum repeat count or returned candidate count:

```powershell
$page = Get-HtmlPage `
    -Url 'https://example.org/events' `
    -MinimumRepeatCount 3 `
    -CollectionLimit 10
```

Skip collection inference when you only need semantic document content:

```powershell
$page = Get-HtmlPage -Url 'https://example.org/article' -NoCollections
```

## Read links and resources

Links expose both resolved and original URLs:

```powershell
$page.Links |
    Select-Object Text, Url, RawUrl, Selector

$internalLinks = $page.Links |
    Where-Object Url -Like 'https://example.org/*'
```

`Resources` comes from the semantic document and is useful for images and
media:

```powershell
$page.Resources |
    Select-Object Kind, Source, AlternateText, MediaType, SourceLocation
```

`Assets` is the web-extraction view, with the raw attribute value and the
resolved URL:

```powershell
$page.Assets |
    Select-Object Name, Value, RawValue, Source, Selector
```

Forms include the form identity, method, resolved action, and discovered
fields:

```powershell
$page.Forms |
    Select-Object Name, Type, RawValue, Value, Selector
```

Use `ConvertFrom-HtmlForm` when the form itself is the task and you want the
specialized form parser directly.

## Read HTML from a variable or file

Use `-BaseUrl` when the supplied HTML contains relative links or image paths:

```powershell
$html = @'
<article>
  <h1>Service status</h1>
  <p>All systems operational.</p>
  <a href="details">Read details</a>
</article>
'@

$page = Get-HtmlPage `
    -Content $html `
    -BaseUrl 'https://example.org/status/'

$page.Links[0].Url
# https://example.org/status/details
```

The same model is available for files:

```powershell
$page = Get-HtmlPage `
    -Path './saved-page.html' `
    -BaseUrl 'https://example.org/'
```

## Use rendered HTML when the static page is empty

`Get-HtmlPage -Url` downloads the server response; it does not execute
JavaScript. If the content appears only after the browser runs, capture a
rendered snapshot and pass that to the same reader:

```powershell
$snapshot = Invoke-HtmlRendering -Url 'https://example.org/app' -Snapshot
$page = Get-HtmlPage -RenderedSnapshot $snapshot

$page.AnalysisMode
# RenderedSnapshot
```

This keeps the object workflow the same whether the input came from static HTML
or a browser-rendered DOM.

## Drop down to selectors only when you need a stable recipe

Collection inference is intended for exploration and one-off extraction. A
website can change its markup, and inferred field names are not a versioned
schema.

When you need a repeatable extraction contract, use the inferred collection to
understand the page, then discover and inspect an explicit selector:

```powershell
$candidate = Find-HtmlSelector `
    -Url 'https://example.org/catalog' `
    -Query 'product' `
    -Limit 1

$candidate | Format-List `
    Selector,
    MatchCount,
    Score,
    Reason,
    Fields,
    SuggestedCommand,
    SuggestedCommandIsReplayable,
    SuggestedCommandNote
```

Review the generated command before running or storing it. Headers, proxy
credentials, and other sensitive request values are intentionally not embedded
in generated scripts.

Use `Select-HtmlData` directly when you already know the stable record and
field selectors:

```powershell
Select-HtmlData `
    -Url 'https://example.org/catalog' `
    -ItemSelector 'article.product-card' `
    -Property @{
        Title = 'h2'
        Price = '.price'
        Link  = @{ Selector = 'a'; Attribute = 'href'; ResolveUrl = $true }
    }
```

## A practical decision path

1. Run `Get-HtmlPage`.
2. Check the summary counts.
3. Use semantic objects for articles and reports.
4. Inspect `Collections` and `Fields` for repeated records.
5. Use `Markdown` only when the consumer needs text.
6. Retry with a rendered snapshot when JavaScript owns the content.
7. Move to `Find-HtmlSelector` and `Select-HtmlData` when you need a reviewed,
   reusable extraction recipe.

The complete deterministic example is available in
[`Examples/Example-GetHtmlPageObjects.ps1`](../../../Examples/Example-GetHtmlPageObjects.ps1).
