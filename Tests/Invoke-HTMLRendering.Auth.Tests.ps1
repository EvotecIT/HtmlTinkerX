# Describe 'Invoke-HTMLRendering with authentication' {
#     It 'Loads content from a basic auth protected page' {
#         $server = Start-Process -FilePath python3 -ArgumentList '-u', (Join-Path $PSScriptRoot 'basic_auth_server.py') -WorkingDirectory $PSScriptRoot -PassThru
#         Start-Sleep -Seconds 1
#         try {
#             $uri = 'http://localhost:8000/Documents/auth.html'
#             $cred = New-Object PSCredential('user', (ConvertTo-SecureString 'pass' -AsPlainText -Force))
#             $html = Invoke-HTMLRendering -Url $uri -Credential $cred
#             $html | Should -Match 'Authenticated'
#         } finally {
#             $server | Stop-Process
#         }
#     }
# }
