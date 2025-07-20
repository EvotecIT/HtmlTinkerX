Import-Module ..\PSParseHTML.psd1 -Force

Get-HTMLResource -Url 'https://example.com' | Out-Null

# Display assembly load contexts used by module assemblies
[AppDomain]::CurrentDomain.GetAssemblies() |
    Where-Object { $_.GetName().Name -like 'HtmlTinkerX*' -or $_.GetName().Name -like 'PSParseHTML.*' } |
    ForEach-Object {
        [pscustomobject]@{
            Name = $_.GetName().Name
            ALC  = [Runtime.Loader.AssemblyLoadContext]::GetLoadContext($_).Name
        }
    } | Format-Table -AutoSize

