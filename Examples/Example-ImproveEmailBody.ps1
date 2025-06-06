Import-Module .\PSParseHTML.psd1 -Force

$Body = EmailBody {
    EmailText -Text 'This is a test email'
}

$Body = Optimize-Email -Body $Body -RemoveComments -RemoveStyleElements
$Body = Format-HTML -Content $Body
$Body