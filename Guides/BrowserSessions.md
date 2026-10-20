# Configure and reuse browser sessions

[Back to the project overview](../README.MD)

## 🔧 Advanced Features

### Restrict a public-source collection session

Use the .NET launch options when a worker visits untrusted public pages. List the
page host and the CDN or API hosts that the page needs:

```csharp
var options = new HtmlBrowserLaunchOptions {
    ChromiumSandbox = true,
    NetworkPolicy = new HtmlBrowserNetworkPolicy(
        strictDestinationHosts: new[] { "publisher.example", "cdn.publisher.example" })
};
await using var session = await HtmlBrowser.OpenSessionForNavigationAsync(options, cancellationToken);
await HtmlBrowser.NavigateAsync(session, "https://publisher.example/advisories", cancellationToken: cancellationToken);
```

This policy requires HTTPS and public destination addresses. Strict destination
hosts are a separate allowlist; the older `AllowedHosts` setting remains an
explicit exception that can permit private hosts in existing PDF workflows.
The browser's managed policy proxy validates the address it connects to, including
redirect destinations. Context routing covers subresources and additional pages.
Restricted sessions block service workers and WebSockets, disable QUIC and
non-proxied WebRTC UDP, and reject persistent profiles, CDP attachment, external
proxies, custom launch arguments, and engines other than Chromium. These options
fail before launch rather than silently losing enforcement. A null `NetworkPolicy`
preserves the existing session behavior.

The restriction applies to the owned session context. Keep browser automation
code trusted: creating another context directly through `session.Browser`, changing
the exposed Playwright routes, or launching another process is outside this
boundary. Run collection workers with their own unprivileged identity, sandbox,
limited filesystem access, deadlines, and process memory/CPU limits. A browser
session does not guarantee that a publisher will permit collection or never show
a human challenge.

For an exact response selected with Playwright's response events, use
`HtmlBrowser.ReadResponseBodyAsync(response, maximumBytes, cancellationToken)`.
It returns original bytes and rejects oversized bodies without truncation or
redaction. Playwright buffers bodies, so this is an acceptance limit rather than
a streaming network or memory quota. The caller still owns response selection,
format validation, cancellation deadlines, and session disposal.

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
