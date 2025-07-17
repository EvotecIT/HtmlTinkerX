Describe 'Save-HTMLAttachment invalid chars' {
    It 'Removes invalid characters from filenames' {
        $root = Join-Path $TestDrive 'web'
        New-Item -ItemType Directory -Path $root | Out-Null
        Set-Content -LiteralPath (Join-Path $root 'bad:name1.txt') -Value '1'
        Set-Content -LiteralPath (Join-Path $root 'bad*name2.txt') -Value '2'
        $html = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <script>
        document.addEventListener('DOMContentLoaded', function() {
            document.getElementById('c1').click();
            document.getElementById('c2').click();
        });
    </script>
</head>
<body>
    <a id='c1' href='bad%3Aname1.txt' download>1</a>
    <a id='c2' href='bad*name2.txt' download>2</a>
</body>
</html>
"@
        $page = Join-Path $root 'index.html'
        Set-Content -Path $page -Value $html
        $server = Start-Process -FilePath python3 -ArgumentList '-u','-m','http.server','8030' -WorkingDirectory $root -PassThru
        Start-Sleep -Seconds 1
        try {
            $uri = 'http://localhost:8030/index.html'
            $dest = Join-Path $TestDrive 'out'
            [array]$files = Save-HTMLAttachment -Url $uri -Path $dest
            $files.Count | Should -Be 2
            $files | Should -Contain (Join-Path $dest 'bad_name1.txt')
            $files | Should -Contain (Join-Path $dest 'bad_name2.txt')
            Test-Path (Join-Path $dest 'bad_name1.txt') | Should -BeTrue
            Test-Path (Join-Path $dest 'bad_name2.txt') | Should -BeTrue
        }
        finally {
            $server | Stop-Process
        }
    }
}
