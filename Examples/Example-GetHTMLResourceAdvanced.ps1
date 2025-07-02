# Download scripts and styles from a webpage
Get-HTMLResource -Url 'https://example.com' -IncludeCss -AsContent | ForEach-Object {
    $_.SaveAsync('./downloads', 'https://example.com').GetAwaiter().GetResult()
}
