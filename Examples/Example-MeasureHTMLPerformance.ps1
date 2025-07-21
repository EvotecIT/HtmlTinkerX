Import-Module ..\PSParseHTML.psd1 -Force

# Measure performance metrics for a web page
$metrics = Measure-HTMLPerformance -Url 'https://example.com'

# Display a brief report
$metrics
