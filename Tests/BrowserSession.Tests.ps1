Describe 'Browser session workflow' {
    It 'Logs in and reuses the session' {
        $server = Start-Process -FilePath python3 -ArgumentList '-u', (Join-Path $PSScriptRoot 'forms_auth_server.py') -WorkingDirectory $PSScriptRoot -PassThru
        Start-Sleep -Seconds 1
        try {
            $base = 'http://localhost:8000'
            $cred = New-Object PSCredential('user', (ConvertTo-SecureString 'pass' -AsPlainText -Force))
            $session = Invoke-HTMLRendering -Url "$base/secret.html" -Credential $cred -LoginUrl "$base/login" -UsernameSelector "input[name=user]" -PasswordSelector "input[name=pass]" -SubmitSelector "input[type=submit]" -Session
            $png = Join-Path $TestDrive 'secure.png'
            Save-HTMLScreenshot -Session $session -OutFile $png -Selector '#secret'
            Test-Path $png | Should -BeTrue
            $null = $session.Page.GotoAsync("$base/Documents/download.html")
            $dest = Join-Path $TestDrive 'dl'
            $files = Save-HTMLDownload -Session $session -Path $dest -Filter 'download.txt'
            Test-Path (Join-Path $dest 'download.txt') | Should -BeTrue
            $files | Should -Contain (Join-Path $dest 'download.txt')
        } finally {
            $session.DisposeAsync().GetAwaiter().GetResult()
            $server | Stop-Process
        }
    }
}
