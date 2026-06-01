Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'ConvertFrom-HtmlRscPayload' {
    BeforeAll {
        $script:Html = @'
<!doctype html>
<html>
<body>
<script>
(self.__next_f=self.__next_f||[]).push([0]);
self.__next_f.push([1,"1:I[\"/app/page.js\",[\"static/chunk.js\"],\"default\"]\n2:HL[\"/style.css\",\"style\"]\n3:{\"name\":\"Ada\"}\n4:T5,hello\n5:{\"after\":true}\n"]);
self.__next_f.push([2,{"field":"value"}]);
</script>
</body>
</html>
'@
    }

    It 'extracts decoded React Flight rows by default' {
        $rows = ConvertFrom-HtmlRscPayload -Content $script:Html

        $rows | Should -HaveCount 5
        $rows[0].Kind | Should -Be 'Module'
        $rows[1].Kind | Should -Be 'Hint'
        $rows[2].Kind | Should -Be 'Model'
        $rows[3].Kind | Should -Be 'Text'
        $rows[3].Data | Should -Be 'hello'
        $rows[4].IsJson | Should -BeTrue
        $rows[4].Json | Should -Be '{"after":true}'
    }

    It 'returns raw payload instructions when requested' {
        $payloads = ConvertFrom-HtmlRscPayload -Content $script:Html -RawPayload

        $payloads | Should -HaveCount 3
        $payloads[0].Kind | Should -Be 'Bootstrap'
        $payloads[1].Kind | Should -Be 'Data'
        $payloads[2].Kind | Should -Be 'FormState'
        $payloads[2].FormStateJson | Should -Be '{"field":"value"}'
    }

    It 'preserves unary literal values in form state payloads' {
        $html = @'
<script>
self.__next_f.push([2,{count:-1,enabled:!0,disabled:!1}]);
</script>
'@

        $payload = ConvertFrom-HtmlRscPayload -Content $html -RawPayload

        $payload.FormStateJson | Should -Be '{"count":-1,"enabled":true,"disabled":false}'
        $payload.RawJson | Should -Be '[2,{"count":-1,"enabled":true,"disabled":false}]'
    }

    It 'returns the full document through the alias' {
        $document = ConvertFrom-HtmlReactFlight -Content $script:Html -AsDocument

        $document.Payloads | Should -HaveCount 3
        $document.Rows | Should -HaveCount 5
    }
}
