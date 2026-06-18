# `Get-HtmlBrowserInteractable`

## 📖 Summary

`Get-HtmlBrowserInteractable` lists all visible or optionally hidden clickable/interactable elements from the current HTML page inside a browser session.
Typical use includes discovering links, buttons, or form elements a user or script might want to interact with — ideal for automation or semi-interactive scripting.

---

## 🧰 Syntax

```powershell
Get-HtmlBrowserInteractable
    [-Session <BrowserSession>]
    [-Url <string>]
    [-Path <string>]
    [-Browser <BrowserEngine>]
    [-Clean]
    [-IncludeHidden]
    [-Limit <int>]
    [-Filter <string>]
```

---

## 📥 Parameters

| Name             | Type     | Required | Description                                                                 |
|------------------|----------|----------|-----------------------------------------------------------------------------|
| `-Session`       | BrowserSession | Yes* | The session object returned by `Start-HtmlBrowserSession`. |
| `-Url`           | string   | Yes* | Load a page at the specified URL and list elements. |
| `-Path`          | string   | Yes* | Load a local HTML file. Alias: `-File`. |
| `-Browser`       | BrowserEngine | No | Choose browser engine when using `-Url` or `-Path`. |
| `-Clean`         | switch   | No | Reinstall browser runtimes for `-Url` or `-Path`. |
| `-IncludeHidden` | switch   | No       | If specified, includes elements not visible to the user (e.g. `display:none`).     |
| `-Limit`         | int      | No       | Limits the number of results returned. Default is 100.                      |
| `-Filter`        | string   | No       | Case-insensitive text filter applied to element text. |

`*` Exactly one of `-Session`, `-Url` or `-Path` must be specified.

---

## 📤 Output

A list of objects with the following structure:

| Property  | Type   | Description                                            |
|-----------|--------|--------------------------------------------------------|
| `Index`   | int    | Order number in result set                             |
| `Text`    | string | Inner text or value of the element                     |
| `Tag`     | string | Tag name (e.g., `a`, `button`, `input`)                |
| `Selector`| string | First 80 characters of the outer HTML (for preview)    |
| `Href`    | string | The `href` or `onclick` attribute value if applicable  |
| `Visible` | bool   | `True` when Playwright reports the element as visible   |
| `PotentiallyHidden` | bool | `True` when the element or an ancestor has attributes or styles that may hide it |

---

## 📋 Example Output

```plaintext
Index Text Visible Selector                                        Href                                      Tag Id Class
----- ---- ------- --------                                        ----                                      --- -- -----
10    Media True    a[href='upload.php']                            upload.php                                a      wp-has-submenu
12    AddMedia True a[href='media-new.php']                         media-new.php                             a
51    Media False   a[href='https://evotec.xyz/wp-admin/media-new.php'] https://evotec.xyz/wp-admin/media-new.php a      ab-item
```

---

## 💡 Examples

### 🧪 Get interactable elements from current page

```powershell
Get-HtmlBrowserInteractable -Session $Session
```

### 🧪 Load a URL directly

```powershell
Get-HtmlBrowserInteractable -Url 'https://example.com'
```

### 🧪 Parse a local HTML file

```powershell
Get-HtmlBrowserInteractable -Path './report.html'
```

### 🧪 Include hidden elements, limit results to 10

```powershell
Get-HtmlBrowserInteractable -Session $Session -IncludeHidden -Limit 10
```

### 🧪 Filter elements and show table output

```powershell
Get-HtmlBrowserInteractable -Session $Session -Filter 'Media' -IncludeHidden | Format-Table
```

### 🧪 Use interactively with `Out-GridView`

```powershell
$pick = Get-HtmlBrowserInteractable -Session $Session | Out-GridView -Title "Pick element" -PassThru
Invoke-HtmlBrowserNavigation -Session $Session -Text $pick.Text
```

---

## 🔧 Internal Behavior

Internally, the following JavaScript logic is executed inside the Playwright context:

```javascript
(() => {
    const isVisible = el => !!( el.offsetWidth || el.offsetHeight || el.getClientRects().length );
    const elements = Array.from(document.querySelectorAll('a, button, input[type="submit"]'));
    return elements
        .filter(el => /* IncludeHidden? */ isVisible(el))
        .map((el, index) => ({
            Index: index,
            Text: el.innerText || el.value || '',
            Tag: el.tagName.toLowerCase(),
            Selector: el.outerHTML.slice(0, 80),
            Href: el.href || el.getAttribute('onclick') || null
        }));
})();
```

This provides a readable list of interactable elements useful for further automation or navigation.

---

## 🧼 Notes

- This cmdlet does not perform any interaction — use `Invoke-HtmlBrowserNavigation` to act on returned elements.
- Use `-Limit` to avoid processing overly large DOM trees.
- Visibility filtering uses bounding box and layout heuristics (`getClientRects()`).

---
