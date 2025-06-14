# `Register-HTMLRoute`

## 📖 Summary

`Register-HTMLRoute` lets you intercept network requests inside a browser session. Use it to block calls or return custom responses when testing pages.

---

## 💡 Example

```powershell
$page = Join-Path $PSScriptRoot 'route_page.html'
$uri = [System.Uri]::new($page).AbsoluteUri
$session = Start-HTMLSession -Url $uri

# Return custom JSON instead of the real file
$handler = Register-HTMLRoute -Session $session -Pattern '*data.json' -ScriptBlock {
    param($route)
    $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions]@{
        Status = 200
        ContentType = 'application/json'
        Body = '{"message":"mock"}'
    }) | Out-Null
}

Invoke-HTMLNavigation -Session $session -Url $uri
Get-HTMLContent -Session $session -Selector '#result' -AsText
# -> 'mock'

Unregister-HTMLRoute -Session $session -Pattern '*data.json' -Handler $handler
Close-HTMLSession -Session $session
```

To simply block a request:

```powershell
Register-HTMLRoute -Session $session -Pattern '*data.json' -ScriptBlock { param($route) $route.AbortAsync() | Out-Null }
```
