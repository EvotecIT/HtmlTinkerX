function Convert-HTMLToText {
    <#
    .SYNOPSIS
    Converts HTML to text.

    .DESCRIPTION
    Converts HTML to text. Simple in it's form it extracts only Text from HTML, regardless of it's structure.

    .PARAMETER File
    Provide HTML file to be converted to PowerShell object.

    .PARAMETER OutputFile
    Parameter description

    .PARAMETER Content
    Provide HTML content to be converted to PowerShell object.

    .PARAMETER URI
    Provide URL to be converted to PowerShell object.

    .EXAMPLE
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

    Convert-HTMLToText -Content $HTMLContentFormatted

    .NOTES
    General notes
    #>
    [CmdletBinding()]
    param(
        [string] $File,
        [string] $OutputFile,
        [Parameter(Mandatory, ParameterSetName = 'Content', ValueFromPipeline, ValueFromPipelineByPropertyName)][string]$Content,
        [alias('Uri')][Parameter(Mandatory, ParameterSetName = 'Uri')][Uri] $Url
    )
    # Load from file or text
    if ($File) {
        if (Test-Path -LiteralPath $File) {
            $Content = [IO.File]::ReadAllText($File)
        } else {
            Write-Warning "Convert-HTMLToText - File doesn't exists"
            return
        }
    } elseif ($Content) {

    } else {
        Write-Warning 'Convert-HTMLToText - No choice file or Content. Termninated.'
        return
    }

    $Output = Convert-InternalHTMLToText -Content $Content

    # Output to file or to text
    if ($OutputFile) {
        [IO.File]::WriteAllText($OutputFile, $Output)
    } else {
        $Output
    }
}