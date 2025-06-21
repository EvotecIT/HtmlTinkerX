# `Get-HTMLConsoleLog`

## 📖 Summary

`Get-HTMLConsoleLog` returns messages printed to the browser console during a session.

---

## 💡 Example

```powershell
$path = Join-Path $PSScriptRoot 'Input/console_page.html'
$uri = [System.Uri]::new($path).AbsoluteUri
$session = Start-HTMLSession -Url $uri
Get-HTMLConsoleLog -Session $session
Close-HTMLSession -Session $session
```

