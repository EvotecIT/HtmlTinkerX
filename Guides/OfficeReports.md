# Turn an HTML table into a supplier review

The [PSWriteOffice supplier-review recipe](https://github.com/EvotecIT/PSWriteOffice/blob/3bad89a2230d4bdb970251e5e1c73065e044ac00/Examples/Integrations/Recipe-HtmlSupplierReview.ps1) combines PSParseHTML table extraction with a Word report, PDF review copy and editable Excel workbook. Its [sample HTML](https://github.com/EvotecIT/PSWriteOffice/blob/3bad89a2230d4bdb970251e5e1c73065e044ac00/Examples/Integrations/Input/SupplierOffers.html) contains four synthetic offers with price and lead-time columns.

Install the modules and check out the exact recipe revision:

```powershell
Install-Module PSParseHTML, PSWriteOffice -Scope CurrentUser
git clone https://github.com/EvotecIT/PSWriteOffice.git PSWriteOffice-reports
git -C PSWriteOffice-reports checkout 3bad89a2230d4bdb970251e5e1c73065e044ac00
Set-Location ./PSWriteOffice-reports

./Examples/Integrations/Recipe-HtmlSupplierReview.ps1 -OutputDirectory ./Output/SupplierReview
```

The extraction boundary is small:

```powershell
$tables = @(ConvertFrom-HtmlTable -Path ./Examples/Integrations/Input/SupplierOffers.html -TableId offers -IncludeMetadata)
if ($tables.Count -ne 1 -or @($tables[0].Data).Count -eq 0) {
    throw 'Supply one nonempty table with id="offers".'
}
$offers = $tables[0].Data
```

`-IncludeMetadata` returns one object per matching table, with rows in `Data`. This lets the recipe reject duplicate table IDs as well as missing or empty tables before creating output. The complete recipe checks for exactly one nonempty result, validates the domain values, and uses invariant numeric parsing for USD prices and integer lead times. It keeps those values numeric in Excel.

To use your own input, keep the `Item`, `Supplier`, `UnitPriceUsd` and `LeadDays` headers and pass `-HtmlPath`. Tax and freight are excluded from the sample contract. PSParseHTML handles the HTML structure; PSWriteOffice handles the document layout. See the [integration guide](https://github.com/EvotecIT/PSWriteOffice/blob/3bad89a2230d4bdb970251e5e1c73065e044ac00/Examples/Integrations/README.md) for requirements and the local Mailozaurr packaging step.
