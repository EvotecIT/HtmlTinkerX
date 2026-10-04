Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Saved DOM extraction recipes' {
    It 'persists typed rules and evaluates fresh HTML through the recipe commands' {
        $recipePath = Join-Path $TestDrive 'products.json'
        Export-HtmlExtractionRecipe -ItemSelector article -Property @{
            Name = @{ Selector = 'span'; Required = $true }
            Price = @{ Selector = 'b'; DataType = [decimal]; Culture = 'pl-PL'; MaximumValueCount = 1 }
            Day = @{ Selector = '.day'; DataType = [DayOfWeek]; DefaultValue = [DayOfWeek]::Monday }
        } -MinimumItemCount 1 -MaximumItemCount 1 -Path $recipePath -PassThru | Should -Be $recipePath
        $recipe = Import-HtmlExtractionRecipe -Path $recipePath
        $recipe.SourceKind | Should -Be 'Dom'
        $result = $recipe | Invoke-HtmlExtractionRecipe -Content '<article><span>Name</span><b>1234,50</b></article>'
        $result.Success | Should -BeTrue
        $result.DomReport.Records[0].Values['Price'] | Should -Be ([decimal]1234.50)
        $result.DomReport.Records[0].Values['Day'] | Should -Be ([DayOfWeek]::Monday)
        $result.DomReport.DefaultedFieldCount | Should -Be 1
        $result.RawContent | Should -BeNullOrEmpty
        $result.Requests.Count | Should -Be 0
    }

    It 'flags changed fields and a missing dataset when the saved recipe is reused' {
        $recipePath = Join-Path $TestDrive 'changed-products.json'
        Export-HtmlExtractionRecipe -ItemSelector article -Property @{
            Name = @{ Selector = 'span'; Required = $true }
            Price = @{ Selector = 'b'; DataType = [decimal]; MaximumValueCount = 1 }
        } -MinimumItemCount 1 -Path $recipePath
        $changed = Invoke-HtmlExtractionRecipe -Path $recipePath -Content '<article><span>Name</span><b>private-invalid-price</b></article>'
        $changed.Success | Should -BeFalse
        $changed.DomReport.Records[0].Values['Name'] | Should -Be 'Name'
        $diagnostic = $changed.DomReport.Fields | Where-Object PropertyName -EQ Price
        $diagnostic.Status.ToString() | Should -Be 'InvalidValue'
        $diagnostic.Error | Should -Not -Match 'private-invalid-price'
        $empty = Invoke-HtmlExtractionRecipe -Path $recipePath -Content ''
        $empty.Success | Should -BeFalse
        $empty.DomReport.ItemCountIsValid | Should -BeFalse
    }

    It 'requires current HTML and rejects an unsupported type before writing a recipe' {
        $recipePath = Join-Path $TestDrive 'requires-content.json'
        Export-HtmlExtractionRecipe -ItemSelector article -Property @{ Name = 'span' } -Path $recipePath
        { Invoke-HtmlExtractionRecipe -Path $recipePath -ErrorAction Stop } | Should -Throw '*current HTML*'
        $invalidPath = Join-Path $TestDrive 'invalid.json'
        { Export-HtmlExtractionRecipe -ItemSelector article -Property @{
            Value = @{ Selector = 'b'; DataType = [object] }
        } -Path $invalidPath -ErrorAction Stop } | Should -Throw '*unsupported data type*'
        Test-Path $invalidPath | Should -BeFalse
    }
}
