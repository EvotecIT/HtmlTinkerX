Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browser profiles' {
    It 'exports profile commands and routes session aliases to the session starter' {
        (Get-Command Start-HtmlBrowserSession).Name | Should -Be 'Start-HtmlBrowserSession'
        (Get-Command New-HtmlBrowserProfile).Name | Should -Be 'New-HtmlBrowserProfile'
        (Get-Command Import-HtmlBrowserProfile).Name | Should -Be 'Import-HtmlBrowserProfile'
        (Get-Command Export-HtmlBrowserProfile).Name | Should -Be 'Export-HtmlBrowserProfile'
        (Get-Command Get-HtmlBrowserSsoHandoff).Name | Should -Be 'Get-HtmlBrowserSsoHandoff'
        (Get-Command Export-HtmlBrowserState).Name | Should -Be 'Export-HtmlBrowserState'
        (Get-Command Import-HtmlBrowserState).Name | Should -Be 'Import-HtmlBrowserState'
        (Get-Command Close-HtmlBrowserOverlay).Name | Should -Be 'Close-HtmlBrowserOverlay'
        (Get-Command Export-HtmlBrowserHar).Name | Should -Be 'Export-HtmlBrowserHar'
        (Get-Alias Start-HtmlSession).Definition | Should -Be 'Start-HtmlBrowserSession'
        (Get-Alias Open-HtmlSession).Definition | Should -Be 'Start-HtmlBrowserSession'
        (Get-Alias Export-BrowserState).Definition | Should -Be 'Export-HtmlBrowserState'
        (Get-Alias Import-BrowserState).Definition | Should -Be 'Import-HtmlBrowserState'
        (Get-Alias Invoke-HtmlBrowserOverlayDismissal).Definition | Should -Be 'Close-HtmlBrowserOverlay'
        (Get-Alias Save-HtmlBrowserHar).Definition | Should -Be 'Export-HtmlBrowserHar'
        $sessionParameters = (Get-Command Start-HtmlBrowserSession).Parameters.Keys
        $sessionParameters | Should -Contain 'LoginUrl'
        $sessionParameters | Should -Contain 'UsernameSelector'
        $sessionParameters | Should -Contain 'PasswordSelector'
        $sessionParameters | Should -Contain 'SubmitSelector'
        $sessionParameters | Should -Contain 'Scenario'
        $sessionParameters | Should -Contain 'ManualLogin'
        $sessionParameters | Should -Contain 'LoginSuccessSelector'
        $sessionParameters | Should -Contain 'LoginTimeout'
        $sessionParameters | Should -Contain 'PreventSsoAutoSubmit'
        $sessionParameters | Should -Contain 'CdpEndpointUrl'
        (Get-Command Start-HtmlBrowserSession).Parameters['CdpEndpointUrl'].Aliases | Should -Contain 'CdpEndpoint'
        (Get-Command Start-HtmlBrowserSession).Parameters['CdpEndpointUrl'].Aliases | Should -Contain 'RemoteDebuggingUrl'
        $profileParameters = (Get-Command New-HtmlBrowserProfile).Parameters.Keys
        $profileParameters | Should -Contain 'LoadState'
        $profileParameters | Should -Contain 'Timeout'
        $profileParameters | Should -Contain 'BlockResourceType'
        $profileParameters | Should -Contain 'BlockResourcePattern'
        $profileParameters | Should -Contain 'BrowserExecutablePath'
        $profileParameters | Should -Contain 'CdpEndpointUrl'
        (Get-Command Export-HtmlBrowserEvidence).Parameters.Keys | Should -Contain 'Scenario'
    }

    It 'round-trips a browser profile through JSON' {
        $profilePath = Join-Path $TestDrive 'work-profile.json'

        $browserProfile = New-HtmlBrowserProfile -Name WorkChrome -Path $profilePath -Scenario AuditProof -UserDataDirectory (Join-Path $TestDrive 'profile') -BrowserChannel chromium -Locale en-US -Timezone UTC -ViewportWidth 1200 -ViewportHeight 800 -LoadState DomContentLoaded -Timeout 45000 -Permission geolocation -BlockResourceType Image,Font -BlockResourcePattern '**/analytics/**' -PreventSsoAutoSubmit
        $loaded = Import-HtmlBrowserProfile -Path $profilePath
        $exportedPath = Join-Path $TestDrive 'exported-profile.json'
        $loaded | Export-HtmlBrowserProfile -Path $exportedPath

        $browserProfile.Name | Should -Be 'WorkChrome'
        $browserProfile.Scenario | Should -Be 'AuditProof'
        $loaded.Name | Should -Be 'WorkChrome'
        $loaded.Scenario | Should -Be 'AuditProof'
        $loaded.BrowserChannel | Should -Be 'chromium'
        $loaded.Locale | Should -Be 'en-US'
        $loaded.Timezone | Should -Be 'UTC'
        $loaded.ViewportWidth | Should -Be 1200
        $loaded.LoadState | Should -Be 'DomContentLoaded'
        $loaded.Timeout | Should -Be 45000
        $loaded.PreventSsoAutoSubmit | Should -BeTrue
        $loaded.Permissions | Should -Contain 'geolocation'
        $loaded.BlockResourceTypes | Should -Contain ([HtmlTinkerX.HtmlNetworkResourceType]::Image)
        $loaded.BlockResourceTypes | Should -Contain ([HtmlTinkerX.HtmlNetworkResourceType]::Font)
        $loaded.BlockResourcePatterns | Should -Contain '**/analytics/**'
        Test-Path -LiteralPath $exportedPath | Should -BeTrue
    }

    It 'round-trips a CDP attached browser profile through JSON' {
        $profilePath = Join-Path $TestDrive 'attached-chrome-profile.json'

        $browserProfile = New-HtmlBrowserProfile -Name AttachedChrome -Path $profilePath -CdpEndpointUrl 'http://127.0.0.1:9222' -PreventSsoAutoSubmit
        $loaded = Import-HtmlBrowserProfile -Path $profilePath

        $browserProfile.CdpEndpointUrl | Should -Be 'http://127.0.0.1:9222'
        $loaded.CdpEndpointUrl | Should -Be 'http://127.0.0.1:9222'
        $loaded.PreventSsoAutoSubmit | Should -BeTrue

        $options = [HtmlTinkerX.HtmlBrowserLaunchOptions]::new()
        $options.ApplyProfile($loaded)
        $options.CdpEndpointUrl | Should -Be 'http://127.0.0.1:9222'
        $options.PreventSsoAutoSubmit | Should -BeTrue
    }

    It 'does not override an existing browser when a profile omits Browser' {
        $profile = New-HtmlBrowserProfile -Name LocaleOnly -Locale en-US
        $options = [HtmlTinkerX.HtmlBrowserLaunchOptions]::new()
        $options.Browser = [HtmlTinkerX.HtmlBrowserEngine]::Firefox

        $options.ApplyProfile($profile)

        $profile.Browser | Should -BeNullOrEmpty
        $options.Browser | Should -Be ([HtmlTinkerX.HtmlBrowserEngine]::Firefox)
        $options.Locale | Should -Be 'en-US'
    }

    It 'rejects mutually exclusive CDP attach launch options before connecting' {
        { Start-HtmlBrowserSession -Url 'about:blank' -CdpEndpointUrl 'http://127.0.0.1:9222' -UserDataDirectory (Join-Path $TestDrive 'profile') -NoDefault } |
            Should -Throw -ExpectedMessage '*do not combine CdpEndpointUrl with UserDataDirectory*'

        { Start-HtmlBrowserSession -Url 'about:blank' -CdpEndpointUrl 'http://127.0.0.1:9222' -StatePath (Join-Path $TestDrive 'state.json') -NoDefault } |
            Should -Throw -ExpectedMessage '*Do not combine CdpEndpointUrl with StatePath*'

        { Start-HtmlBrowserSession -Url 'about:blank' -CdpEndpointUrl 'http://127.0.0.1:9222' -BrowserChannel chrome -NoDefault } |
            Should -Throw -ExpectedMessage '*BrowserChannel, BrowserExecutablePath, and Clean are not used*'

        { Start-HtmlBrowserSession -Url 'about:blank' -CdpEndpointUrl 'http://127.0.0.1:9222' -UserAgent 'IgnoredUserAgent' -NoDefault } |
            Should -Throw -ExpectedMessage '*context options such as Proxy, UserAgent, Locale, viewport, geolocation, timezone, and permissions are not applied*'
    }

    It 'rejects document resource blocking in reusable browser profiles' {
        { New-HtmlBrowserProfile -Name BadProfile -BlockResourceType Document } |
            Should -Throw -ExpectedMessage '*BlockResourceType Document would abort page navigation*'
    }

    It 'rejects document resource blocking when starting a browser session' {
        $pagePath = Join-Path $TestDrive 'session-document-block.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><main>blocked</main>'

        { Start-HtmlBrowserSession -Path $pagePath -BlockResourceType Document -NoDefault } |
            Should -Throw -ExpectedMessage '*BlockResourceType Document would abort page navigation*'
    }

    It 'starts sessions from profile launch defaults and lets explicit parameters override profile values' {
        $pagePath = Join-Path $TestDrive 'profile-launch-session.html'
        $profilePath = Join-Path $TestDrive 'profile-launch.json'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><main>profile launch</main>'
        New-HtmlBrowserProfile -Name ProfileLaunch -Path $profilePath -Scenario AuditProof -ViewportWidth 1111 -ViewportHeight 777 -LoadState DomContentLoaded | Out-Null

        $session = Start-HtmlBrowserSession -Path $pagePath -ProfilePath $profilePath -ViewportWidth 1200 -NoDefault
        try {
            $viewport = Invoke-HtmlBrowserScript -Session $session -Script '() => ({ width: window.innerWidth, height: window.innerHeight })'

            [int]$viewport.width | Should -Be 1200
            [int]$viewport.height | Should -Be 777
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'applies browser scenario defaults before explicit profile values' {
        $options = [HtmlTinkerX.HtmlBrowserLaunchOptions]::new()
        $options.ApplyScenario([HtmlTinkerX.HtmlBrowserScenario]::LowBandwidth)

        $options.Scenario | Should -Be 'LowBandwidth'
        $options.LoadState | Should -Be 'DomContentLoaded'
        $options.Timeout | Should -Be 30000
        $options.BlockResourceTypes | Should -Contain ([HtmlTinkerX.HtmlNetworkResourceType]::Image)
        $options.BlockResourceTypes | Should -Contain ([HtmlTinkerX.HtmlNetworkResourceType]::Font)

        $profile = [HtmlTinkerX.HtmlBrowserProfile]::new()
        $profile.Scenario = [HtmlTinkerX.HtmlBrowserScenario]::AuditProof
        $profile.ViewportWidth = 1200
        $profile.ViewportHeight = 800
        $profile.LoadState = [HtmlTinkerX.HtmlBrowserLoadState]::DomContentLoaded
        $profile.Timeout = 45000
        $profile.BlockResourceTypes.Add([HtmlTinkerX.HtmlNetworkResourceType]::Script)
        $profile.BlockResourcePatterns.Add('**/tracking/**')

        $profileOptions = [HtmlTinkerX.HtmlBrowserLaunchOptions]::new()
        $profileOptions.ApplyProfile($profile)

        $profileOptions.Scenario | Should -Be 'AuditProof'
        $profileOptions.ViewportWidth | Should -Be 1200
        $profileOptions.ViewportHeight | Should -Be 800
        $profileOptions.ScreenWidth | Should -Be 1366
        $profileOptions.ScreenHeight | Should -Be 900
        $profileOptions.LoadState | Should -Be 'DomContentLoaded'
        $profileOptions.Timeout | Should -Be 45000
        $profileOptions.BlockResourceTypes | Should -Contain ([HtmlTinkerX.HtmlNetworkResourceType]::Script)
        $profileOptions.BlockResourcePatterns | Should -Contain '**/tracking/**'
    }

    It 'clears scenario defaults when explicit scenario overrides a profile scenario' {
        $profile = [HtmlTinkerX.HtmlBrowserProfile]::new()
        $profile.Scenario = [HtmlTinkerX.HtmlBrowserScenario]::LowBandwidth
        $profile.BlockResourceTypes.Add([HtmlTinkerX.HtmlNetworkResourceType]::Script)

        $options = [HtmlTinkerX.HtmlBrowserLaunchOptions]::new()
        $options.ApplyProfile($profile)
        $options.ApplyScenario([HtmlTinkerX.HtmlBrowserScenario]::Custom)

        $options.Scenario | Should -Be 'Custom'
        $options.LoadState | Should -Be 'NetworkIdle'
        $options.Timeout | Should -Be 10000
        $options.BlockResourceTypes | Should -Contain ([HtmlTinkerX.HtmlNetworkResourceType]::Script)
        $options.BlockResourceTypes | Should -Not -Contain ([HtmlTinkerX.HtmlNetworkResourceType]::Image)
        $options.BlockResourceTypes | Should -Not -Contain ([HtmlTinkerX.HtmlNetworkResourceType]::Media)
        $options.BlockResourceTypes | Should -Not -Contain ([HtmlTinkerX.HtmlNetworkResourceType]::Font)
    }

    It 'lets explicit state path and proxy parameters clear inherited profile launch values' {
        $assembly = [PSParseHTML.PowerShell.CmdletStartHtmlBrowserSession].Assembly
        $requestType = $assembly.GetType('PSParseHTML.PowerShell.HtmlBrowserLaunchOptionRequest', $true)
        $factoryType = $assembly.GetType('PSParseHTML.PowerShell.HtmlBrowserLaunchOptionFactory', $true)
        $method = $factoryType.GetMethod('CreateAsync', [System.Reflection.BindingFlags] 'Public,Static')

        $baseOptions = [HtmlTinkerX.HtmlBrowserLaunchOptions]::new()
        $baseOptions.UserDataDirectory = Join-Path $TestDrive 'profile'
        $baseOptions.Proxy = 'http://old-proxy:8080'
        $baseOptions.ProxyUsername = 'old-user'
        $baseOptions.ProxyPassword = 'old-password'

        $request = [System.Activator]::CreateInstance($requestType, $true)
        $requestType.GetProperty('BaseOptions').SetValue($request, $baseOptions)
        $requestType.GetProperty('BoundParameters').SetValue($request, [hashtable] @{
            StatePath = $true
            Proxy     = $true
        })
        $requestType.GetProperty('StatePath').SetValue($request, (Join-Path $TestDrive 'state.json'))
        $requestType.GetProperty('Proxy').SetValue($request, 'http://new-proxy:8080')

        $task = $method.Invoke($null, @($request, [System.Threading.CancellationToken]::None))
        $options = $task.GetAwaiter().GetResult()

        $options.StorageStatePath | Should -Match 'state\.json$'
        $options.UserDataDirectory | Should -BeNullOrEmpty
        $options.Proxy | Should -Be 'http://new-proxy:8080'
        $options.ProxyUsername | Should -BeNullOrEmpty
        $options.ProxyPassword | Should -BeNullOrEmpty

        $profileProxyOptions = [HtmlTinkerX.HtmlBrowserLaunchOptions]::new()
        $profileProxyOptions.Proxy = 'http://profile-proxy:8080'
        $profileProxyRequest = [System.Activator]::CreateInstance($requestType, $true)
        $requestType.GetProperty('BaseOptions').SetValue($profileProxyRequest, $profileProxyOptions)
        $requestType.GetProperty('BoundParameters').SetValue($profileProxyRequest, [hashtable] @{
            ProxyCredential = $true
        })
        $proxyCredential = [pscredential]::new('profile-user', (ConvertTo-SecureString 'profile-password' -AsPlainText -Force))
        $requestType.GetProperty('ProxyCredential').SetValue($profileProxyRequest, $proxyCredential)

        $profileProxyTask = $method.Invoke($null, @($profileProxyRequest, [System.Threading.CancellationToken]::None))
        $profileProxyLaunch = $profileProxyTask.GetAwaiter().GetResult()

        $profileProxyLaunch.Proxy | Should -Be 'http://profile-proxy:8080'
        $profileProxyLaunch.ProxyUsername | Should -Be 'profile-user'
        $profileProxyLaunch.ProxyPassword | Should -Be 'profile-password'

        $cdpOptions = [HtmlTinkerX.HtmlBrowserLaunchOptions]::new()
        $cdpOptions.CdpEndpointUrl = 'http://127.0.0.1:9222'

        $cdpRequest = [System.Activator]::CreateInstance($requestType, $true)
        $requestType.GetProperty('BaseOptions').SetValue($cdpRequest, $cdpOptions)
        $requestType.GetProperty('BoundParameters').SetValue($cdpRequest, [hashtable] @{
            BrowserChannel = $true
        })
        $requestType.GetProperty('BrowserChannel').SetValue($cdpRequest, 'chrome')

        $cdpTask = $method.Invoke($null, @($cdpRequest, [System.Threading.CancellationToken]::None))
        $cdpLaunch = $cdpTask.GetAwaiter().GetResult()

        $cdpLaunch.CdpEndpointUrl | Should -BeNullOrEmpty
        $cdpLaunch.BrowserChannel | Should -Be 'chrome'

        $targetOptions = [HtmlTinkerX.HtmlBrowserLaunchOptions]::new()
        $targetOptions.BrowserExecutablePath = Join-Path $TestDrive 'profile-chrome.exe'

        $channelRequest = [System.Activator]::CreateInstance($requestType, $true)
        $requestType.GetProperty('BaseOptions').SetValue($channelRequest, $targetOptions)
        $requestType.GetProperty('BoundParameters').SetValue($channelRequest, [hashtable] @{
            BrowserChannel = $true
        })
        $requestType.GetProperty('BrowserChannel').SetValue($channelRequest, 'msedge')

        $channelTask = $method.Invoke($null, @($channelRequest, [System.Threading.CancellationToken]::None))
        $channelLaunch = $channelTask.GetAwaiter().GetResult()

        $channelLaunch.BrowserChannel | Should -Be 'msedge'
        $channelLaunch.BrowserExecutablePath | Should -BeNullOrEmpty

        $pathOptions = [HtmlTinkerX.HtmlBrowserLaunchOptions]::new()
        $pathOptions.BrowserChannel = 'chrome'
        $explicitPath = Join-Path $TestDrive 'explicit-browser.exe'

        $pathRequest = [System.Activator]::CreateInstance($requestType, $true)
        $requestType.GetProperty('BaseOptions').SetValue($pathRequest, $pathOptions)
        $requestType.GetProperty('BoundParameters').SetValue($pathRequest, [hashtable] @{
            BrowserExecutablePath = $true
        })
        $requestType.GetProperty('BrowserExecutablePath').SetValue($pathRequest, $explicitPath)

        $pathTask = $method.Invoke($null, @($pathRequest, [System.Threading.CancellationToken]::None))
        $pathLaunch = $pathTask.GetAwaiter().GetResult()

        $pathLaunch.BrowserExecutablePath | Should -Match 'explicit-browser\.exe$'
        $pathLaunch.BrowserChannel | Should -BeNullOrEmpty
    }
}
