Describe 'Optimize-HTML' {
    It 'Given formatted HTML content - Should minimize it' {
        $HTMLContentFormatted = @"
<html>
        <!-- HEADER -->
        <head>
                <meta charset="utf-8">
                <meta content="width=device-width, initial-scale=1" name="viewport">
                <meta name="author">
                <meta content="2019-08-09 11:26:37" name="revised">
                <title>My title</title>
                <!-- CSS Default fonts START -->
                <link href="https://fonts.googleapis.com/css?family=Roboto|Hammersmith+One|Questrial|Oswald" type="text/css" rel="stylesheet">
                <!-- CSS Default fonts END -->
                <!-- CSS Default fonts icons START -->
                <link href="https://use.fontawesome.com/releases/v5.7.2/css/all.css" type="text/css" rel="stylesheet">
                <!-- CSS Default fonts icons END -->
        </head>
        <body>
                <div class="flexElement overflowHidden">
                        <table id="DT-hZRTQIVT" class="display compact">
                                <thead>
                                        <tr>
                                                <th>Name</th>
                                                <th class="none">Id</th>
                                                <th class="none">HandleCount</th>
                                                <th>WorkingSet</th>
                                        </tr>
                                </thead>
                                <tbody>
                                        <tr>
                                                <td>1Password</td>
                                                <td>22268</td>
                                                <td>1007</td>
                                                <td>87146496</td>
                                        </tr>
                                        <tr>
                                                <td>aesm_service</td>
                                                <td>25340</td>
                                                <td>189</td>
                                                <td>3948544</td>
                                        </tr>
                                </tbody>
                        </table>
                </div>
                <footer></footer>
                <!-- END FOOTER -->
        </body>
        <!-- END BODY -->
        <!-- FOOTER -->
</html>
"@

        $ExpectedOutput = @"
<html><head><meta charset=utf-8 /><meta content="width=device-width, initial-scale=1" name=viewport /><meta name=author /><meta content="2019-08-09 11:26:37" name=revised /><title>My title</title><link href="https://fonts.googleapis.com/css?family=Roboto|Hammersmith+One|Questrial|Oswald" type=text/css rel=stylesheet /><link href=https://use.fontawesome.com/releases/v5.7.2/css/all.css type=text/css rel=stylesheet /></head><body><div class="flexElement overflowHidden"><table id=DT-hZRTQIVT class="display compact"><thead><tr><th>Name</th><th class=none>Id</th><th class=none>HandleCount</th><th>WorkingSet</th></tr></thead><tbody><tr><td>1Password</td><td>22268</td><td>1007</td><td>87146496</td></tr><tr><td>aesm_service</td><td>25340</td><td>189</td><td>3948544</td></tr></tbody></table></div><footer></footer></body></html>
"@

        $Minimized = Optimize-HTML -Content $HTMLContentFormatted -CSSDecodeEscapes:$false -TreatAsDocument -RemoveComments
        $Minimized | Should -Be $ExpectedOutput

        $file = Join-Path $TestDrive 'test.html'
        $HTMLContentFormatted | Set-Content -Path $file
        $fromFile = Optimize-HTML -File $file -TreatAsDocument -RemoveComments
        $fromFile | Should -Be $ExpectedOutput
    }

    It 'Defaults to fragment mode' {
        $fragment = '<tr></tr>'
        $result = Optimize-HTML -Content $fragment -CSSDecodeEscapes:$false
        $result | Should -Be $fragment
    }

    It 'Can treat input as document' {
        $content = '<html><!--c--><body> <p>Hi</p></body></html>'
        $result = Optimize-HTML -Content $content -TreatAsDocument -RemoveComments
        $result | Should -Be '<html><body><p>Hi</p></body></html>'
    }

    It 'Keeps comments by default' {
        $content = '<html><!--c--><body>Hi</body></html>'
        $result = Optimize-HTML -Content $content -TreatAsDocument
        $result | Should -Match '<!--c-->'
    }

    It 'Removes comments when requested' {
        $content = '<html><!--c--><body>Hi</body></html>'
        $result = Optimize-HTML -Content $content -TreatAsDocument -RemoveComments
        $result | Should -Not -Match '<!--c-->'
    }

    It 'Removes optional tags when requested' {
        $content = '<html><body><p>Hi</p></body></html>'
        $result = Optimize-HTML -Content $content -TreatAsDocument -RemoveOptionalTags
        $result | Should -Not -Match '</p>'
    }
}
