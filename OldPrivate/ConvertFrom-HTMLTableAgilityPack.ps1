function ConvertFrom-HTMLTableAgilityPack {
    [cmdletbinding()]
    param(
        [Uri] $Url,
        [string] $Content,
        [System.Collections.IDictionary] $ReplaceContent,
        [System.Collections.IDictionary] $ReplaceHeaders,
        [switch] $ReverseTable
    )
    Begin {
        # Workaround for Agility Pack
        # https://www.codetable.net/decimal/173
        $Replacements = @{
            "&#60;"  = "<";
            "&#62;"  = ">";
            "&#32;"  = " ";
            "&#31;"  = "?";
            "&#34;"  = "\";
            "&#39;"  = "'";
            "&#38;"  = "&";
            "&#40;"  = "(";
            "&#41;"  = ")";
            "&#58;"  = ":";
            "&#59;"  = ";";
            "&#61;"  = "=";
            "&#91;"  = "[";
            "&#93;"  = "]";
            "&#123;" = "{";
            "&#125;" = "}";
            "&#124;" = "|";
            "&#160;" = " ";
            "&#173;" = "-";
            "&amp;"  = "&";
        }
    }
    Process {
        if ($Content) {
            [HtmlAgilityPack.HtmlDocument] $HtmlDocument = [HtmlAgilityPack.HtmlDocument]::new()
            $HtmlDocument.LoadHtml($Content)
        } else {
            # It seems there's a problem with detecting encoding in HAP
            # https://github.com/zzzprojects/html-agility-pack/issues/320
            # The workaround is to load the page once to get encoding
            # and once loaded, reload to get with prtoper encoding
            [HtmlAgilityPack.HtmlWeb] $HtmlWeb = [HtmlAgilityPack.HtmlWeb]::new()
            [HtmlAgilityPack.HtmlDocument] $HtmlDocument = $HtmlWeb.Load($url)
            $DetectedEncoding = $HtmlDocument.Encoding

            # Workaround for HAP bug
            [HtmlAgilityPack.HtmlWeb] $HtmlWeb = [HtmlAgilityPack.HtmlWeb]::new()
            $HtmlWeb.AutoDetectEncoding = $false
            $HtmlWeb.OverrideEncoding = $DetectedEncoding
            [HtmlAgilityPack.HtmlDocument] $HtmlDocument = $HtmlWeb.Load($url)

            # try {
            #     $Content = (Invoke-WebRequest -Uri $UrL -ErrorAction Stop).Content
            # } catch {
            #     Write-Warning "ConvertFrom-HTMLTableAgilityPack - Error loading $Url. Errror: $($_.Exception.Message)"
            #     return
            # }
            # [HtmlAgilityPack.HtmlDocument] $HtmlDocument = [HtmlAgilityPack.HtmlDocument]::new()
            # $HtmlDocument.LoadHtml($Content)
        }
        [Array] $Tables = $HtmlDocument.DocumentNode.SelectNodes("//table")


        [Array] $OutputTables = :table foreach ($table in $Tables) {
            $Rows = $table.SelectNodes('.//tr')
            #$Rows | Format-Table -Property ChildNodes,InnerHtml, Line
            if ($ReverseTable) {
                $Count = 0
                [Array] $TableContent = @(
                    $obj = [ordered] @{ }
                    $TableContent = foreach ($Row in $Rows) {
                        $Count++

                        [string] $CellHeader = $row.SelectNodes("th").InnerText
                        # Converting to Unicode Decimal Code
                        foreach ($R in $Replacements.Keys) {
                            $CellHeader = $CellHeader -replace $R, $Replacements[$R]
                        }

                        [string] $CellContent = $row.SelectNodes("td").InnerText
                        $CellContent = $CellContent.Trim()
                        if ($ReplaceContent) {
                            foreach ($Key in $ReplaceContent.Keys) {
                                $CellContent = $CellContent -replace $Key, $ReplaceContent[$Key]
                            }
                        }
                        # Converting to Unicode Decimal Code
                        foreach ($R in $Replacements.Keys) {
                            $CellContent = $CellContent -replace $R, $Replacements[$R]
                        }
                        # Assign to object
                        if ($CellHeader) {
                            $obj["$($CellHeader)"] = $CellContent
                        } else {
                            $obj["$Count"] = $CellContent
                        }
                    }
                    $obj
                )
            } else {
                $Headers = foreach ($Row in $Rows[0]) {
                    foreach ($Cell in $row.SelectNodes("th|td")) {
                        $CellContent = $Cell.InnerText.Trim()
                        if ($ReplaceHeaders) {
                            foreach ($Key in $ReplaceHeaders.Keys) {
                                $CellContent = $CellContent -replace $Key, $ReplaceHeaders.$Key
                            }
                        }
                        # Converting to Unicode Decimal Code to get rid of special chars like &#160;
                        foreach ($R in $Replacements.Keys) {
                            $CellContent = $CellContent -replace $R, $Replacements[$R]
                        }
                        $CellContent
                    }
                }
                $TableContent = foreach ($Row in $Rows | Select-Object -Skip 1) {
                    $obj = [ordered] @{ }
                    for ($x = 0; $x -lt $headers.count; $x++) {
                        [string] $CellContent = $row.SelectNodes("th|td")[$x].InnerText
                        $CellContent = $CellContent.Trim()
                        if ($ReplaceContent) {
                            foreach ($Key in $ReplaceContent.Keys) {
                                $CellContent = $CellContent -replace $Key, $ReplaceContent.$Key
                            }
                        }
                        # Converting to Unicode Decimal Code to get rid of special chars like &#160;
                        foreach ($R in $Replacements.Keys) {
                            $CellContent = $CellContent -replace $R, $Replacements[$R]
                        }
                        # Assign to object
                        if ($($headers[$x])) {
                            $obj["$($headers[$x])"] = $CellContent
                        } else {
                            $obj["$x"] = $CellContent
                        }
                    }
                    [PSCustomObject] $obj
                }
            }
            @(, $TableContent)
        }
        $OutputTables
    }
    End { }
}