# Dependency Policy

## SixLabors packages

Keep the SixLabors image stack on the 2.x-compatible package line:

- `SixLabors.ImageSharp` `2.1.13`
- `SixLabors.ImageSharp.Drawing` `1.0.0`
- `SixLabors.Fonts` `1.0.1`

Do not upgrade `SixLabors.ImageSharp` to 3.x or `SixLabors.ImageSharp.Drawing` to 2.x as part of routine dependency cleanup. Those versions move the dependency graph onto the newer SixLabors license model. A future upgrade should be a deliberate project decision with an explicit license review, not an automatic package bump.

This means `dotnet list package --outdated` is expected to report these packages as outdated. Treat that as an intentional pin unless the license decision changes.

The package references live in `Sources/HtmlTinkerX/HtmlTinkerX.csproj` and apply to every target framework, including `net472`, `net8.0`, and `net10.0`.
