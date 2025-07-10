Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

describe 'Invoke-HTMLClick' {
    it 'Validates ClickCount range' {
        { Invoke-HTMLClick -Selector '#demo' -ClickCount 0 } | Should -Throw
    }
}
