Describe 'ConvertFrom-HtmlForm' {
    It 'Parses sample forms using AngleSharp' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_form.html'
        $content = Get-Content -LiteralPath $path -Raw
        $forms = ConvertFrom-HtmlForm -Content $content
        $forms.Count | Should -Be 2
        $forms[0].Action | Should -Be '/login'
        $forms[0].Method | Should -Be 'POST'
        $forms[0].Fields[0].Name | Should -Be 'user'
        $forms[0].Fields[1].Type | Should -Be ([HtmlTinkerX.HtmlFormFieldType]::Password)
    }

    It 'Preserves successful repeated values and resolves actions from the document address' {
        $html = @'
<base href="/actions/">
<input form="prefs" name="outside" value="yes">
<form id="prefs" action="save" method="post">
<input name="repeat" value="one"><input name="repeat" value="two">
<input name="disabled" disabled value="keep-in-inventory">
<input form="" name="unowned" value="omit">
<select name="chosen"><option selected value="a">A</option><option selected value="b">B</option></select>
</form>
'@
        $form = ConvertFrom-HtmlForm -Content $html -BaseUri 'https://example.test/page' -IncludeMetadata
        $form.Action | Should -Be 'save'
        $form.ResolvedAction | Should -Be 'https://example.test/actions/save'
        $form.SourceUrl | Should -Be 'https://example.test/page'
        $form.FinalUrl | Should -Be 'https://example.test/page'
        $form.BaseUrl | Should -Be 'https://example.test/actions/'
        ($form.Fields.Name -join ',') | Should -Be 'outside,repeat,repeat,disabled,chosen'
        (($form.SuccessfulFields | ForEach-Object { $_.Key + '=' + $_.Value }) -join ',') |
            Should -Be 'outside=yes,repeat=one,repeat=two,chosen=b'
    }

    It 'Returns the current selected radio value in the public submission list' {
        $form = ConvertFrom-HtmlForm -Content '<form><input type="radio" name="choice" value="a" checked><input type="radio" name="choice" value="b" checked></form>'

        $form.Fields.Count | Should -Be 2
        $form.Fields[0].Value | Should -Be ''
        $form.Fields[1].Value | Should -Be 'b'
        $form.SuccessfulFields.Count | Should -Be 1
        $form.SuccessfulFields[0].Key | Should -Be 'choice'
        $form.SuccessfulFields[0].Value | Should -Be 'b'
    }

    It 'Rejects a relative document address for content action resolution' {
        { ConvertFrom-HtmlForm -Content '<form action="save"></form>' -BaseUri '/relative' -ErrorAction Stop } |
            Should -Throw '*absolute*'
    }
}
