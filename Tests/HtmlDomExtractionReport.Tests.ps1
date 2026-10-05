Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Static extraction quality reports' {
    It 'keeps valid fields and reports invalid and missing fields' {
        $report = Select-HtmlData -Content '<article><b>private-invalid-price</b><span>Name</span></article>' `
            -ItemSelector article -Property @{
                Name = 'span'
                Price = @{ Selector = 'b'; DataType = [decimal] }
                Required = @{ Selector = '.missing'; Required = $true }
            } -AsExtractionReport -MinimumItemCount 1
        $report.GetType().FullName | Should -Be 'HtmlTinkerX.HtmlDomExtractionReport'
        $report.IsValid | Should -BeFalse
        $report.ItemCount | Should -Be 1
        $report.InvalidFieldCount | Should -Be 2
        $report.Records[0].Values['Name'] | Should -Be 'Name'
        $report.Records[0].Values['Price'] | Should -BeNullOrEmpty
        ($report.Fields | Where-Object PropertyName -EQ Price).Status.ToString() | Should -Be 'InvalidValue'
        ($report.Fields | Where-Object PropertyName -EQ Price).Error | Should -Not -Match 'private-invalid-price'
    }

    It 'detects duplicate field values and a dataset that no longer contains items' {
        $report = Select-HtmlData -Content '<article><b>12</b><b>24</b></article>' -ItemSelector article `
            -Property @{ Price = @{ Selector = 'b'; DataType = [decimal]; MaximumValueCount = 1 } } `
            -AsExtractionReport -MaximumItemCount 1
        $report.Fields[0].Status.ToString() | Should -Be 'TooManyValues'
        $report.Fields[0].ValueCount | Should -Be 2
        $empty = Select-HtmlData -Content '<main></main>' -ItemSelector article -Property @{ Name = 'span' } `
            -AsExtractionReport -MinimumItemCount 1
        $empty.IsValid | Should -BeFalse
        $empty.ItemCount | Should -Be 0
    }

    It 'applies declared value counts to ordinary extraction as well' {
        { Select-HtmlData -Content '<article><b>12</b><b>24</b></article>' -ItemSelector article `
            -Property @{ Price = @{ Selector = 'b'; MaximumValueCount = 1 } } -ErrorAction Stop } |
            Should -Throw '*Price*count bounds*'
    }
}
