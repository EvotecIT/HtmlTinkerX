# `Get-HtmlBrowserConsoleLog`

## 📖 Summary

`Get-HtmlBrowserConsoleLog` returns messages printed to the browser console during a session.

---

## 💡 Example

```powershell
$path = Join-Path $PSScriptRoot 'Input/console_page.html'
$uri = [System.Uri]::new($path).AbsoluteUri
$session = Start-HtmlBrowserSession -Url $uri
Get-HtmlBrowserConsoleLog -Session $session
Close-HtmlBrowserSession -Session $session
```

