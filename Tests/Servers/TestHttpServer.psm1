# Simple local HTTP server helper for Pester tests

function Get-FreeTcpPort {
    param(
        [System.Net.IPAddress]$Address = [System.Net.IPAddress]::Loopback
    )
    $listener = [System.Net.Sockets.TcpListener]::new($Address, 0)
    try {
        $listener.Start()
        return ($listener.LocalEndpoint).Port
    } finally {
        $listener.Stop()
    }
}

function Wait-UntilPortOpen {
    param(
        [string]$Host = '127.0.0.1',
        [int]$Port,
        [int]$TimeoutSeconds = 20
    )
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        try {
            $c = [System.Net.Sockets.TcpClient]::new()
            $c.Connect($Host, $Port)
            $c.Dispose()
            return $true
        } catch {
            if ($sw.Elapsed.TotalSeconds -ge $TimeoutSeconds) { return $false }
            Start-Sleep -Milliseconds 200
        }
    }
}

function Start-TestHttpServer {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string]$Root,
        [int]$TimeoutSeconds = 20
    )

    $py = Get-Command python3 -ErrorAction SilentlyContinue
    if (-not $py) { return $null }

    $port = Get-FreeTcpPort
    $args = @('-u','-m','http.server',$port,'--bind','127.0.0.1')
    $proc = Start-Process -FilePath $py.Source -ArgumentList $args -WorkingDirectory $Root -PassThru
    if (-not (Wait-UntilPortOpen -Port $port -TimeoutSeconds $TimeoutSeconds)) {
        try { if ($proc -and -not $proc.HasExited) { $proc | Stop-Process -Force } } catch {}
        throw 'HTTP server failed to start.'
    }
    [pscustomobject]@{
        Process = $proc
        Port    = $port
        BaseUrl = "http://127.0.0.1:$port"
        Root    = (Resolve-Path -LiteralPath $Root).Path
    }
}

function Stop-TestHttpServer {
    [CmdletBinding()] param([Parameter(ValueFromPipeline,Mandatory)] $Server)
    process {
        try { if ($Server.Process -and -not $Server.Process.HasExited) { $Server.Process | Stop-Process -Force } } catch {}
    }
}

Export-ModuleMember -Function Start-TestHttpServer,Stop-TestHttpServer

