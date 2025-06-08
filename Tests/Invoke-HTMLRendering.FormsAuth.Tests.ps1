# Describe 'Invoke-HTMLRendering with form authentication' {
#     It 'Loads content from a form protected page' {
#         $server = Start-Process -FilePath python3 -ArgumentList '-u', (Join-Path $PSScriptRoot 'forms_auth_server.py') -WorkingDirectory $PSScriptRoot -PassThru
#         Start-Sleep -Seconds 1
#         try {
#             $uri = 'http://localhost:8000/secret.html'
#             $cred = New-Object PSCredential('user', (ConvertTo-SecureString 'pass' -AsPlainText -Force))
#             $html = Invoke-HTMLRendering -Url $uri -Credential $cred -LoginUrl 'http://localhost:8000/login' -UsernameSelector "input[name=user]" -PasswordSelector "input[name=pass]" -SubmitSelector "input[type=submit]"
#             $html | Should -Match 'Authenticated'
#         }
#         finally {
#             $server | Stop-Process
#         }
#     }
# }
