# Configure and reuse browser sessions

[Back to the project overview](../README.MD)

## 🔧 Advanced Features

### Browser Configuration
```powershell
# Custom browser settings
$session = Start-HtmlBrowserSession -Url 'https://example.com' `
    -UserAgent 'Custom Bot 1.0' `
    -ViewportWidth 1920 `
    -ViewportHeight 1080 `
    -DeviceScaleFactor 2 `
    -Visible `
    -SlowMo 1000
```

### Request Interception
```powershell
# Mock API responses
$handler = Register-HtmlRoute -Session $session -Pattern '**/api/data' -ScriptBlock {
    param($route)
    Complete-HtmlRoute -Route $route -Options @{
        Status = 200
        ContentType = 'application/json'
        Body = '{"status": "success", "data": []}'
    }
}

# Navigate and test
Invoke-HtmlBrowserNavigation -Session $session -Url 'https://example.com/app'
Unregister-HtmlRoute -Session $session -Pattern '**/api/data' -Handler $handler
```

### State Management
```powershell
# Save browser state
Export-HtmlBrowserState -Session $session -Path 'session-state.json'

# Restore in new session
$newSession = Import-HtmlBrowserState -Path 'session-state.json' -Url 'https://example.com/dashboard'
```
