Import-Module .\PSParseHTML.psd1 -Force

$File = "$PSScriptRoot\..\Examples\Input\azure_status.html"
$Objects = ConvertFrom-HTMLAttributes -Url 'https://evotec.xyz' -Tag 'meta' -ReturnObject
$Table = foreach ($O in $Objects) {
    [PSCUstomObject] @{
        name    = $O.name
        content = $O.content
        comment = $O.comment
    }
}
$Table | Format-Table -AutoSize


$Objects1 = ConvertFrom-HTMLAttributes -Content (Get-Content -Raw $File) -Tag 'meta' -ReturnObject
$Table = foreach ($O in $Objects1) {
    [PSCUstomObject] @{
        name    = $O.name
        content = $O.content
        comment = $O.comment
    }
}
$Table | Format-Table -AutoSize