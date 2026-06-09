Describe 'Get-HTMLFormField' {
    It 'Returns fields from sample HTML' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_form.html'
        $content = Get-Content -LiteralPath $path -Raw
        $fields = Get-HTMLFormField -Content $content
        $fields.Count | Should -Be 3
        $fields[0].Name | Should -Be 'user'
        $fields[0].Type | Should -Be ([HtmlTinkerX.HtmlFormFieldType]::Text)
    }

    It 'Returns field values from sample HTML' {
        $content = @'
<form>
  <input type="hidden" name="csrf" value="abc">
  <input type="text" name="user" value="Ada">
  <textarea name="notes">hello</textarea>
  <select name="tier">
    <option value="basic">Basic</option>
    <option value="pro" selected>Pro</option>
  </select>
  <input type="checkbox" name="remember" checked>
</form>
'@
        $fields = Get-HTMLFormField -Content $content

        ($fields | Where-Object Name -EQ 'csrf').Value | Should -Be 'abc'
        ($fields | Where-Object Name -EQ 'user').Value | Should -Be 'Ada'
        ($fields | Where-Object Name -EQ 'notes').Value | Should -Be 'hello'
        ($fields | Where-Object Name -EQ 'tier').Value | Should -Be 'pro'
        ($fields | Where-Object Name -EQ 'remember').Value | Should -Be 'on'
    }
}
