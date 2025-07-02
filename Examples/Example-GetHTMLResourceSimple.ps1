# Extract external script URLs from a local file
$links = Get-HTMLResource -Path './Tests/Documents/azure_status.html'
$links | Select-Object Type, Source, Comment
