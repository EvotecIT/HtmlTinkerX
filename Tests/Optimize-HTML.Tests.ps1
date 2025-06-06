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

        $Minimized = Optimize-HTML -Content $HTMLContentFormatted -CSSDecodeEscapes:$false
        $Minimized | Should -Be $ExpectedOutput

        $file = Join-Path $TestDrive 'test.html'
        $HTMLContentFormatted | Set-Content -Path $file
        $fromFile = Optimize-HTML -File $file
        $fromFile | Should -Be $ExpectedOutput
    }
}