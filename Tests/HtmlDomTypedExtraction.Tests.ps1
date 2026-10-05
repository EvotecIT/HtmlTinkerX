Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Typed static DOM extraction' {
    It 'converts the selector map to decimal, integer, Boolean, date, and enum values' {
        $items = Select-HtmlData -Content "<article data-price='1234,50' data-count='42' data-active='true' data-day='monday' data-date='2026-10-05T12:30:00+02:00'></article>" `
            -ItemSelector article -Property @{
                Price = @{ Self = $true; Attribute = 'data-price'; DataType = [decimal]; Culture = 'pl-PL' }
                Count = @{ Self = $true; Attribute = 'data-count'; DataType = [long] }
                Active = @{ Self = $true; Attribute = 'data-active'; DataType = [bool] }
                Day = @{ Self = $true; Attribute = 'data-day'; DataType = [DayOfWeek] }
                Date = @{ Self = $true; Attribute = 'data-date'; DataType = [DateTimeOffset] }
            }

        $items.Price | Should -BeOfType ([decimal])
        $items.Price | Should -Be 1234.50
        $items.Count | Should -BeOfType ([long])
        $items.Count | Should -Be 42
        $items.Active | Should -BeTrue
        $items.Day | Should -Be ([DayOfWeek]::Monday)
        $items.Date | Should -BeOfType ([DateTimeOffset])
        $items.Date.Offset | Should -Be ([TimeSpan]::FromHours(2))
    }

    It 'returns typed arrays and uses a typed default for optional empty fields' {
        $item = Select-HtmlData -Content '<article><span>12</span><span>24</span><b> </b></article>' `
            -ItemSelector article -Property @{
                Counts = @{ Selector = 'span'; DataType = [int]; All = $true }
                Default = @{ Selector = 'b'; DataType = [decimal]; TreatEmptyAsMissing = $true; DefaultValue = '12,5'; Culture = 'pl-PL' }
            }
        $item.Counts | Should -HaveCount 2
        $item.Counts[0] | Should -BeOfType ([int])
        $item.Counts[1] | Should -Be 24
        $item.Default | Should -BeOfType ([decimal])
        $item.Default | Should -Be 12.5
    }

    It 'reports the property and item when conversion fails without echoing the value' {
        $message = $null
        try {
            Select-HtmlData -Content '<article>private-invalid-number</article>' -ItemSelector article `
                -Property @{ Price = @{ Self = $true; DataType = [decimal] } } -ErrorAction Stop
        } catch {
            $message = $_.Exception.Message
        }
        $message | Should -Match 'Price.*item 0.*Decimal'
        $message | Should -Not -Match 'private-invalid-number'
    }

    It 'rejects a required empty field even when a default is supplied' {
        { Select-HtmlData -Content '<article> </article>' -ItemSelector article `
            -Property @{ Price = @{ Self = $true; DataType = [decimal]; Required = $true; TreatEmptyAsMissing = $true; DefaultValue = 0 } } `
            -ErrorAction Stop } | Should -Throw '*Required property*Price*item 0*'
    }

    It 'requires an explicit .NET type in the PowerShell field map' {
        { Select-HtmlData -Content '<article>12</article>' -ItemSelector article `
            -Property @{ Price = @{ Self = $true; DataType = 'Decimal' } } -ErrorAction Stop } |
            Should -Throw '*DataType*Price*.NET type*'
    }
}
