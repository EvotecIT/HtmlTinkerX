# `Get-HTMLInteractable`

## 📖 Summary

`Get-HTMLInteractable` lists all visible or optionally hidden clickable/interactable elements from the current HTML page inside a browser session.  
Typical use includes discovering links, buttons, or form elements a user or script might want to interact with — ideal for automation or semi-interactive scripting.

---

## 🧰 Syntax

```powershell
Get-HTMLInteractable
    [-Session <BrowserSession>]
    [-IncludeHidden]
    [-Limit <int>]
```

---

## 📥 Parameters

| Name             | Type     | Required | Description                                                                 |
|------------------|----------|----------|-----------------------------------------------------------------------------|
| `-Session`       | BrowserSession | Yes | The session object returned by `Start-HTMLSession`. |
| `-IncludeHidden` | switch   | No       | If specified, includes elements not visible to the user (e.g. `display:none`).     |
| `-Limit`         | int      | No       | Limits the number of results returned. Default is 100.                      |

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

---

## 📋 Example Output

```plaintext
Index  Text         Tag     Selector                            Href
-----  ----         ---     --------                            ----
0      Login        button  <button type="submit">              null
1      Dashboard    a       <a href="/admin">Dashboard</a>      /admin
2      Settings     a       <a href="/settings">Settings</a>    /settings
```

---

## 💡 Examples

### 🧪 Get interactable elements from current page

```powershell
Get-HTMLInteractable -Session $Session
```

### 🧪 Include hidden elements, limit results to 10

```powershell
Get-HTMLInteractable -Session $Session -IncludeHidden -Limit 10
```

### 🧪 Use interactively with `Out-GridView`

```powershell
$pick = Get-HTMLInteractable -Session $Session | Out-GridView -Title "Pick element" -PassThru
Invoke-HTMLNavigation -Session $Session -Text $pick.Text
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

- This cmdlet does not perform any interaction — use `Invoke-HTMLNavigation` to act on returned elements.
- Use `-Limit` to avoid processing overly large DOM trees.
- Visibility filtering uses bounding box and layout heuristics (`getClientRects()`).

---
