Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'HTML selector cmdlets' {
    It 'returns one empty value for an attribute without a value' {
        $document = [HtmlAgilityPack.HtmlDocument]::new()
        $attribute = $document.CreateAttribute('empty')

        $values = @($attribute | Select-HtmlAttributeValue -DefaultValue 'fallback' -ErrorAction Stop)
        $values.Count | Should -Be 1
        $values[0] | Should -Be ''
    }

    It 'uses the requested default for a valueless attribute treated as missing' {
        $document = [HtmlAgilityPack.HtmlDocument]::new()
        $attribute = $document.CreateAttribute('empty')

        ($attribute | Select-HtmlAttributeValue -TreatEmptyAsMissing -DefaultValue 'fallback' -ErrorAction Stop) | Should -Be 'fallback'
    }

    It 'normalizes valueless attribute text before applying text options' -TestCases @(
        @{ Options = @{}; Expected = '' }
        @{ Options = @{ NoTrim = $true; DeEntitize = $true; DefaultValue = 'fallback' }; Expected = 'fallback' }
    ) {
        param($Options, $Expected)
        $document = [HtmlAgilityPack.HtmlDocument]::new()
        $attribute = $document.CreateAttribute('empty')

        $values = @($attribute | Select-HtmlInnerText @Options -ErrorAction Stop)
        $values.Count | Should -Be 1
        $values[0] | Should -Be $Expected
    }

    BeforeAll {
        $script:Html = @'
<!doctype html>
<html>
<body>
    <h3 class="muiTypography-root title-main">Main heading</h3>
    <p id="intro" data-state="ready">Tom &amp; Jerry</p>
    <a href="/reports/42" data-kind="report">Report</a>
    <a href="/help" data-kind="support">Help</a>
</body>
</html>
'@
    }

    It 'selects nodes with XPath and Single' {
        $node = ConvertFrom-HTML -Content $script:Html | Select-HtmlNode -XPath '//p[@id="intro"]' -Single

        $node | Should -Not -BeNullOrEmpty
        $node.Name | Should -Be 'p'
        ($node | Select-HtmlAttributeValue -AttributeName 'data-state') | Should -Be 'ready'
    }

    It 'selects nodes by tag and attribute equality' {
        $node = ConvertFrom-HTML -Content $script:Html |
            Select-HtmlNode -Tag a -AttributeName data-kind -AttributeValue report -Single

        ($node | Select-HtmlAttributeValue -AttributeName href) | Should -Be '/reports/42'
    }

    It 'supports contains and starts-with helper predicates' {
        $heading = ConvertFrom-HTML -Content $script:Html |
            Select-HtmlNode -Tag h3 -AttributeName class -AttributeValue title -Contains -Single
        $link = ConvertFrom-HTML -Content $script:Html |
            Select-HtmlNode -Tag a -AttributeName href -AttributeValue /reports -StartsWith -Single

        ($heading | Select-HtmlInnerText) | Should -Be 'Main heading'
        ($link | Select-HtmlInnerText) | Should -Be 'Report'
    }

    It 'can return matching text nodes' {
        $text = ConvertFrom-HTML -Content $script:Html |
            Select-HtmlNode -Tag a -AttributeName data-kind -AttributeValue support -Text -Single

        $text.InnerText | Should -Be 'Help'
    }

    It 'returns defaults for missing attributes' {
        $node = ConvertFrom-HTML -Content $script:Html | Select-HtmlNode -XPath '//p' -Single

        ($node | Select-HtmlAttributeValue -AttributeName missing -DefaultValue 'fallback') | Should -Be 'fallback'
    }

    It 'decodes HTML entities in inner text' {
        $node = ConvertFrom-HTML -Content $script:Html | Select-HtmlNode -XPath '//p' -Single

        ($node | Select-HtmlInnerText -DeEntitize) | Should -Be 'Tom & Jerry'
    }

    It 'can inspect a matching object property as an attribute value' {
        $object = [pscustomobject]@{
            href = '/object-link'
        }

        ($object | Select-HtmlAttributeValue -AttributeName href) | Should -Be '/object-link'
    }
}
