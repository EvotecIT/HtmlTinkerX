# Turn an HTML table into a supplier review

The [PSWriteOffice supplier-review recipe](https://github.com/EvotecIT/PSWriteOffice/blob/main/Examples/Integrations/Recipe-HtmlSupplierReview.ps1) combines PSParseHTML table extraction with a Word report, PDF review copy and editable Excel workbook. Its [sample HTML](https://github.com/EvotecIT/PSWriteOffice/blob/main/Examples/Integrations/Input/SupplierOffers.html) contains four synthetic offers with price and lead-time columns.

Install the modules, then run the recipe from a PSWriteOffice checkout:

```powershell
Install-Module PSParseHTML, PSWriteOffice -Scope CurrentUser
./Examples/Integrations/Recipe-HtmlSupplierReview.ps1 -OutputDirectory ./Output/SupplierReview
```

The extraction boundary is small:

```powershell
$tables = @(ConvertFrom-HtmlTable -Path ./SupplierOffers.html -TableId offers)
if ($tables.Count -ne 1 -or $tables[0].Count -eq 0) {
    throw 'Supply one nonempty table with id="offers".'
}
$offers = $tables[0]
```

`ConvertFrom-HtmlTable` returns a collection of tables. Select the first table only after checking that the requested table exists. The complete recipe checks for exactly one nonempty result, validates the domain values, and uses invariant numeric parsing for USD prices and integer lead times. It keeps those values numeric in Excel.

To use your own input, keep the `Item`, `Supplier`, `UnitPriceUsd` and `LeadDays` headers and pass `-HtmlPath`. Tax and freight are excluded from the sample contract. PSParseHTML handles the HTML structure; PSWriteOffice handles the document layout. See the [integration guide](https://github.com/EvotecIT/PSWriteOffice/blob/main/Examples/Integrations/README.md) for requirements and the local Mailozaurr packaging step.
