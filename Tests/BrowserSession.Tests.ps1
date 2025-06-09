            $session = Start-HTMLSession -Url "$base/secret.html" -Credential $cred -LoginUrl "$base/login" -UsernameSelector "input[name=user]" -PasswordSelector "input[name=pass]" -SubmitSelector "input[type=submit]"
            Invoke-HTMLNavigation -Session $session -Url "$base/secret.html" |
                Save-HTMLScreenshot -OutFile $png -Selector '#secret'
            Invoke-HTMLNavigation -Session $session -Url "$base/Documents/download.html" |
                Save-HTMLAttachment -Path $dest -Filter 'download.txt' | Out-Null
            Close-HTMLSession -Session $session
#             $cred = New-Object PSCredential('user', (ConvertTo-SecureString 'pass' -AsPlainText -Force))
#             $session = Invoke-HTMLRendering -Url "$base/secret.html" -Credential $cred -LoginUrl "$base/login" -UsernameSelector "input[name=user]" -PasswordSelector "input[name=pass]" -SubmitSelector "input[type=submit]" -Session
#             $png = Join-Path $TestDrive 'secure.png'
#             Save-HTMLScreenshot -Session $session -OutFile $png -Selector '#secret'
#             Test-Path $png | Should -BeTrue
#             $null = $session.Page.GotoAsync("$base/Documents/download.html")
#             $dest = Join-Path $TestDrive 'dl'
#             $files = Save-HTMLDownload -Session $session -Path $dest -Filter 'download.txt'
#             Test-Path (Join-Path $dest 'download.txt') | Should -BeTrue
#             $files | Should -Contain (Join-Path $dest 'download.txt')
#         } finally {
#             $session.DisposeAsync().GetAwaiter().GetResult()
#             $server | Stop-Process
#         }
#     }
# }
