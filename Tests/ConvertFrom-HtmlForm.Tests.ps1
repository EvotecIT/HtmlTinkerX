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

    It 'Resolves an absolute HTML base without adding source metadata' {
        $forms = ConvertFrom-HtmlForm -Content '<base href="https://example.test/forms/"><form action="save"></form><form></form>' -IncludeMetadata

        $forms[0].BaseUrl | Should -Be 'https://example.test/forms/'
        $forms[0].ResolvedAction | Should -Be 'https://example.test/forms/save'
        $forms[0].SourceUrl | Should -BeNullOrEmpty
        $forms[0].FinalUrl | Should -BeNullOrEmpty
        $forms[1].ResolvedAction | Should -BeNullOrEmpty
    }

    It 'Ignores a rejected first HTML base when resolving a relative action' {
        $form = ConvertFrom-HtmlForm -Content '<base href="javascript:alert(1)"><base href="https://other.test/"><form action="save"></form>' -BaseUri 'https://example.test/account/page' -IncludeMetadata

        $form.BaseUrl | Should -Be 'https://example.test/account/page'
        $form.ResolvedAction | Should -Be 'https://example.test/account/save'
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
