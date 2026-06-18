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

    It 'rejects mutually exclusive CDP attach launch options before connecting' {
        { Start-HtmlBrowserSession -Url 'about:blank' -CdpEndpointUrl 'http://127.0.0.1:9222' -UserDataDirectory (Join-Path $TestDrive 'profile') -NoDefault } |
            Should -Throw -ExpectedMessage '*do not combine CdpEndpointUrl with UserDataDirectory*'

        { Start-HtmlBrowserSession -Url 'about:blank' -CdpEndpointUrl 'http://127.0.0.1:9222' -StatePath (Join-Path $TestDrive 'state.json') -NoDefault } |
            Should -Throw -ExpectedMessage '*Do not combine CdpEndpointUrl with StatePath*'

        { Start-HtmlBrowserSession -Url 'about:blank' -CdpEndpointUrl 'http://127.0.0.1:9222' -BrowserChannel chrome -NoDefault } |
            Should -Throw -ExpectedMessage '*BrowserChannel, BrowserExecutablePath, and Clean are not used*'
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
}
