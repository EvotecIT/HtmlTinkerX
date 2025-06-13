## ChangeLog

#### 1.0.2 - 2024.02.19
- Better detection of net framework

#### 1.0.1 - 2024.02.19
- Again refreshed dependencies

#### 1.0.0 - 2024.02.19
- Rebuilt how the module is built and how it's loaded
- Refreshed dependencies

#### 0.0.25 - 2023.01.15
- Fixes library loading issue

#### 0.0.24 - 2022.12.31
- 👍 Update nuget HtmlAgilityPack to `1.11.46`
- 👍 Update nuget Nuglify to `1.20.4`
- 🪲 Fixes `ConvertFrom-HTML` when used with Content

#### 0.0.22 - 2022.05.01
  - Fixes a conflict with the DLL's not properly loading in both **Windows PowerShell** and **PowerShell 6/7** scenarios
  - Using separate DLLs for **Windows PowerShell** and **PowerShell 6/7**
  - Added detection of net framework and requirement to 4.7.2 when using Windows PowerShell

#### 0.0.21 - 2022.05.01
  - Fixes issue with ConvertFrom-HTMLTable when using AgilityPack. Using **AgilityPack** for HTML parsing would fail to detect encoding or convert special chars. This release fixes this.
  - Updates libraries:
    - AngleSharp to version 0.16.1.0
    - AngleSharp.Css to version 0.16.4.0
    - HtmlAgilityPack to version 1.11.42.0
    - NUglify to version 1.12.0.0

#### 0.0.20 - 2022.01.16
  - Added ConvertTo-HTML function

#### 0.0.19 - 2021.03.14
  - Update NUglify to 1.13.6
#### 0.0.18 - 2021.02.02
  - Update NUglify to 1.13.2
  - Update HtmlAgilityPack to v1.11.30
  - Better formatting HTML (including JS/CSS)
  - Better minimizing HTML (including JS/CSS)
  - Fixes ConvertFrom-HtmlTable for some use cases
#### 0.0.17 - 2020.10.21
  - Update NUglify to 1.9.9
#### 0.0.16 - 2020.08.21
  - Improvements to ConvertFrom-HTMLAttributes
#### 0.0.14 - 2020.02.12
  - Added ConvertFrom-HTMLAttributes. Can search for Tags/Classes
  - Removed ConvertFrom-HTMLTag and added it as an alias to ConvertFrom-HTMLAttributes
#### 0.0.13 - 2020.01.04
  - Fix for Optimize-HTML - it wasn't working at all
#### 0.0.9 - 10.10.2019
  - Updated ConvertFrom-HTMLTable fixing problem with the header being part of the table (fix by Anthony Howell!)
#### 0.0.8 - 8.10.2019
  - Updated ConvertFrom-HTMLTable to support more tables
  - Added ConvertFrom-HTMLTag
#### 0.0.6 - 6.10.2019
  - Added ConvertFrom-HTMLTable (Thank you - Anthony Howell!)
#### 0.0.5 - 11.08.2019 - [Blog post for the release](https://evotec.xyz/formatting-and-minifying-resources-html-css-javascript-with-powershell/)
  - First public release