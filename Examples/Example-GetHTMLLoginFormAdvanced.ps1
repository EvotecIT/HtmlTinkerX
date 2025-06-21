Import-Module .\PSParseHTML.psd1 -Force

$session = Start-HTMLSession -Url 'https://example.com/login'
$form = Get-HTMLLoginForm -Session $session
if ($form) {
    Invoke-HTMLRendering -Session $session -LoginUrl $form.LoginUrl `
        -UsernameSelector $form.UsernameSelector `
        -PasswordSelector $form.PasswordSelector `
        -SubmitSelector $form.SubmitSelector `
        -Credential (Get-Credential)
}
Close-HTMLSession -Session $session
