[CmdletBinding()]
param(
    [string] $RecipePath = (Join-Path $PSScriptRoot 'Output\recorded-browser.recipe.json'),
    [string] $EvidencePath = (Join-Path $PSScriptRoot 'Output\recorded-browser-evidence')
)

Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force

$recipeDirectory = Split-Path -Parent $RecipePath
if ($recipeDirectory -and -not (Test-Path -LiteralPath $recipeDirectory)) {
    New-Item -ItemType Directory -Path $recipeDirectory -Force | Out-Null
}

$pagePath = Join-Path $PSScriptRoot 'Output\recordable-browser-page.html'
@'
<!doctype html>
<html>
<head><title>Recipe recording demo</title></head>
<body>
  <main>
    <label for="search">Mailbox search</label>
    <input id="search" name="q" />
    <label for="password">Password-like field</label>
    <input id="password" name="password" type="password" />
    <label><input id="include" type="checkbox" /> Include archive</label>
    <select id="scope">
      <option value="inbox">Inbox</option>
      <option value="archive">Archive</option>
    </select>
    <button id="load" onclick="document.getElementById('results').textContent = document.getElementById('search').value + ':' + document.getElementById('scope').value + ':' + document.getElementById('include').checked;">Load</button>
    <section id="results">Waiting</section>
  </main>
</body>
</html>
'@ | Set-Content -LiteralPath $pagePath -Encoding UTF8

$session = Start-HtmlBrowserSession -Url ([System.Uri]::new($pagePath).AbsoluteUri) -Scenario SinglePageApp
try {
    Start-HtmlBrowserRecipeRecording -Session $session -Name 'RecordedMailboxProof' -IncludeCurrentUrl | Out-Null
    Set-HtmlBrowserInput -Session $session -Selector '#search' -Value 'mailbox'
    Set-HtmlBrowserInput -Session $session -Selector '#password' -Value 'demo-secret-not-saved'
    Set-HtmlBrowserChecked -Session $session -Selector '#include'
    Set-HtmlBrowserSelectOption -Session $session -Selector '#scope' -Value archive
    Invoke-HtmlBrowserClick -Session $session -Selector '#load'
    Wait-HtmlBrowserContent -Session $session -Selector '#results' -Text 'mailbox:archive:true' -Exact
    Find-HtmlBrowserLocator -Session $session -Query 'Load' -Limit 3 | Out-Null
    Export-HtmlBrowserEvidence -Session $session -OutFolder $EvidencePath -BaseFileName recorded-proof -Artifact Html,Text -NoManifest | Out-Null

    $recipe = Stop-HtmlBrowserRecipeRecording -Session $session -Path $RecipePath -PassThru
} finally {
    Close-HtmlBrowserSession -Session $session
}

$replay = Invoke-HtmlBrowserRecipe -Path $RecipePath -Variable @{ password = 'runtime-demo-secret' }

[pscustomobject] @{
    RecipePath     = $RecipePath
    RecipeName     = $recipe.Name
    RecordedSteps  = @($recipe.Steps).Count
    RedactedSteps   = @($recipe.Steps | Where-Object ValueRedacted).Count
    EvidencePath   = $EvidencePath
    ReplaySucceeded = $replay.Succeeded
    ReplaySteps    = @($replay.Steps).Count
    FinalUrl       = $replay.FinalUrl
}
