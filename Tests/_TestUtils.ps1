function Get-FreeTcpPort {
    [CmdletBinding()]
    param(
        [string]$Address = '127.0.0.1'
    )
    $listener = $null
    try {
        $ip = [System.Net.IPAddress]::Parse($Address)
        $listener = [System.Net.Sockets.TcpListener]::new($ip, 0)
        $listener.Start()
        $port = ($listener.LocalEndpoint -as [System.Net.IPEndPoint]).Port
        return $port
    } finally {
        if ($listener) { $listener.Stop() }
    }
}

function Wait-RecordedFrame {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        $Session,
        [int]$ExtraDelayMs = 150
    )
    if (-not $Session) { return }
    try {
        # Prefer a deterministic DOM change used by our test page
        $null = $Session.Page.WaitForSelectorAsync('#loaded').GetAwaiter().GetResult()
    } catch {}
    try {
        # Nudge layout slightly to encourage a fresh frame
        $null = $Session.Page.EvaluateAsync('()=>{ window.scrollTo(0,1); return 0; }', $null).GetAwaiter().GetResult()
    } catch {}
    try {
        # Ensure at least one render tick has happened
        $null = $Session.Page.EvaluateAsync('new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)))', $null).GetAwaiter().GetResult()
    } catch {}
    if ($ExtraDelayMs -gt 0) { Start-Sleep -Milliseconds $ExtraDelayMs }
}

