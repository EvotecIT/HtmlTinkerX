Describe 'Table grid extraction' {
    It 'Keeps nested tables separate with <Engine>' -ForEach @(@{ Engine = 'AngleSharp' }, @{ Engine = 'AgilityPack' }) {
        $html = '<table><tr><th>A</th><th>B</th></tr><tbody><tr><td rowspan="0">Group</td><td>1<table><tr><th>Inner</th></tr><tr><td>Value</td></tr></table></td></tr><tr><td>2</td></tr></tbody><tbody><tr><td>Next</td><td>3</td></tr></tbody></table>'
        $tables = ConvertFrom-HtmlTable -Content $html -Engine $Engine -IncludeMetadata
        $tables.Count | Should -Be 2
        $tables[0].Data.Count | Should -Be 3
        $tables[0].Data[1].A | Should -Be 'Group'
        $tables[0].Data[1].B | Should -Be '2'
        $tables[0].Data[2].A | Should -Be 'Next'
        $tables[1].Data[0].Inner | Should -Be 'Value'
    }

    It 'Applies explicit grid limits to content and files with <Engine>' -ForEach @(@{ Engine = 'AngleSharp' }, @{ Engine = 'AgilityPack' }) {
        $html = '<table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>'
        $file = Join-Path $TestDrive 'table.html'
        Set-Content -LiteralPath $file -Value $html -Encoding UTF8
        { ConvertFrom-HtmlTable -Content $html -Engine $Engine -MaximumColumns 1 -ErrorAction Stop } | Should -Throw
        { ConvertFrom-HtmlTable -Path $file -Engine $Engine -MaximumExpandedCells 1 -ErrorAction Stop } | Should -Throw
        $table = ConvertFrom-HtmlTable -Path $file -Engine $Engine -MaximumExpandedCells 2 -AsDataTable
        $table.Columns.Count | Should -Be 2
        $table.Rows[0].A | Should -Be '1'
        $table.Rows[0].B | Should -Be '2'
    }
}
