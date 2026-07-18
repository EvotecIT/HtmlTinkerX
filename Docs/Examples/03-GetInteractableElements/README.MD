# `Get-HtmlBrowserInteractable`

`Get-HtmlBrowserInteractable` returns controls that can be inspected or acted
on in a browser session. It can use an existing session, open a URL, or open a
local HTML file.

For the complete parameter sets, browser profile options, and authentication
parameters, see the generated
[`Get-HtmlBrowserInteractable` command reference](../../Get-HtmlBrowserInteractable.md).

## Common syntax

```powershell
Get-HtmlBrowserInteractable
    [-Session <HtmlBrowserSession>]
    [-Url <string>]
    [-Path <string>]
    [-IncludeHidden]
    [-Limit <int>]
    [-Filter <string>]
```

Use one input mode:

- `-Session` inspects an existing browser session. When omitted in this
  parameter set, the cmdlet uses the module's default session.
- `-Url` opens a URL in a temporary session.
- `-Path` opens a local HTML file. `-File` is an alias.

`-IncludeHidden` keeps elements Playwright reports as not visible. `-Limit`
defaults to 100. `-Filter` performs a case-insensitive match against element
text.

## Output

Each `HtmlInteractableInfo` result includes:

| Property | Description |
| --- | --- |
| `Index`, `Text`, `Tag`, `Id`, `Class` | Element identity and visible text |
| `Selector` | CSS selector that identifies the element |
| `Href` | Link target when applicable |
| `Visible`, `PotentiallyHidden` | Playwright visibility and possible accessibility hiding |
| `Enabled`, `Editable`, `InViewport` | Current interaction state |
| `X`, `Y`, `Width`, `Height` | Viewport geometry in CSS pixels |

## Examples

```powershell
# Inspect the current or default session
Get-HtmlBrowserInteractable -Session $session

# Open a URL or local file for inspection
Get-HtmlBrowserInteractable -Url 'https://example.com'
Get-HtmlBrowserInteractable -Path './report.html'

# Narrow a large page
Get-HtmlBrowserInteractable -Session $session -Filter 'Media' -Limit 10
```

The returned selector is a starting point for interaction. A selector based on
a shared class, link target, or tag can match more than one element, so inspect
those matches and pass the selected zero-based index through `-Nth`:

```powershell
$pick = Get-HtmlBrowserInteractable -Session $session |
    Out-GridView -Title 'Pick an element' -OutputMode Single

if ($pick) {
    $selectorMatches = try {
        @(
            Get-HtmlBrowserElement -Session $session -Selector $pick.Selector `
                -IncludeAttributes -ErrorAction Stop
        )
    } catch {
        @()
    }

    if ($selectorMatches.Count -eq 0) {
        Write-Warning 'The selected element is no longer present on the page.'
    } else {
        $target = if ($selectorMatches.Count -eq 1) {
            $selectorMatches[0]
        } else {
            $selectorMatches |
                Out-GridView -Title 'Choose the exact matching element' -OutputMode Single
        }

        if ($target) {
            Invoke-HtmlBrowserClick -Session $session -Selector $pick.Selector -Nth $target.Index
        }
    }
}
```

This cmdlet only inspects the page. Use commands such as
`Invoke-HtmlBrowserClick`, `Set-HtmlBrowserInput`, or
`Invoke-HtmlBrowserNavigation` to change browser state.
