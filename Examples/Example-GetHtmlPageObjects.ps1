$html = @'
<!doctype html>
<html lang="en">
<head>
  <title>Service report</title>
  <base href="https://example.org/reports/">
</head>
<body>
  <main>
    <h1>Service report</h1>
    <p>All systems operational.</p>
    <table>
      <caption>Availability</caption>
      <thead>
        <tr><th>Service</th><th>Status</th></tr>
      </thead>
      <tbody>
        <tr><td>API</td><td>Healthy</td></tr>
      </tbody>
    </table>
    <a href="details">Read details</a>
    <section class="incident-card">
      <h2>Incident 42</h2>
      <span class="severity">Low</span>
      <a href="incidents/42">Open</a>
    </section>
    <section class="incident-card">
      <h2>Incident 43</h2>
      <span class="severity">High</span>
      <a href="incidents/43">Open</a>
    </section>
  </main>
</body>
</html>
'@

$page = Get-HtmlPage `
    -Content $html `
    -BaseUrl 'https://example.org/reports/'

Write-Host 'Page summary' -ForegroundColor Cyan
$page | Format-List `
    Title,
    Language,
    AnalysisMode,
    HeadingCount,
    ParagraphCount,
    TableCount,
    CollectionCount

Write-Host 'Headings' -ForegroundColor Cyan
$page.Headings |
    Select-Object Level, Text |
    Format-Table -AutoSize

Write-Host 'Links' -ForegroundColor Cyan
$page.Links |
    Select-Object Text, Url |
    Format-Table -AutoSize

Write-Host 'Table rows' -ForegroundColor Cyan
$page.Tables[0].Rows | ForEach-Object {
    [pscustomobject]@{
        Cells = ($_.Cells.Text -join ' | ')
    }
} | Format-Table -AutoSize

$incidents = $page.Collections |
    Where-Object {
        $_.Fields.Name -contains 'Title' -and
        $_.Fields.Name -contains 'Severity'
    } |
    Select-Object -First 1

Write-Host 'Inferred collection' -ForegroundColor Cyan
$incidents |
    Select-Object Name, Count, Confidence,
        @{ Name = 'Fields'; Expression = { $_.Fields.Name -join ', ' } } |
    Format-Table -AutoSize

Write-Host 'Collection items' -ForegroundColor Cyan
$incidents.Items |
    Select-Object Title, Severity, Link |
    Format-Table -AutoSize

Write-Host 'Markdown projection' -ForegroundColor Cyan
$page.Markdown
