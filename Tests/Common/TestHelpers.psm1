Import-Module (Join-Path $PSScriptRoot '..' 'Servers' 'TestHttpServer.psm1') -Force

function Initialize-TestSite {
    [CmdletBinding()]
    param(
        [Parameter()][string]$Root = $PSScriptRoot,
        [Parameter()][int]$TimeoutSeconds = 20
    )
    $usingLocal = $false
    $baseUrl = $null
    $server = Start-TestHttpServer -Root $Root -TimeoutSeconds $TimeoutSeconds
    if ($server) { $usingLocal = $true; $baseUrl = $server.BaseUrl }
    [pscustomobject]@{
        UsingLocalServer = $usingLocal
        BaseUrl          = $baseUrl
        Root             = (Resolve-Path -LiteralPath $Root).Path
        Server           = $server
    }
}

function Get-TestUrl {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Site,
        [Parameter(Mandatory)][string]$RelativePath
    )
    if ($Site.UsingLocalServer -and $Site.BaseUrl) {
        return ($Site.BaseUrl.TrimEnd('/') + '/' + $RelativePath.TrimStart('/'))
    }
    $abs = Join-Path $Site.Root $RelativePath
    return [System.Uri]::new($abs).AbsoluteUri
}

function Cleanup-TestSite {
    [CmdletBinding()] param([Parameter(Mandatory,ValueFromPipeline)][object]$Site)
    process { if ($Site.Server) { $Site.Server | Stop-TestHttpServer } }
}

Export-ModuleMember -Function Initialize-TestSite,Get-TestUrl,Cleanup-TestSite

