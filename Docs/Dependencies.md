# Dependency Policy

## SixLabors packages

Keep the SixLabors image stack on the 2.x-compatible package line:

- `SixLabors.ImageSharp` `2.1.13`
- `SixLabors.ImageSharp.Drawing` `1.0.0`
- `SixLabors.Fonts` `1.0.1`

Do not upgrade `SixLabors.ImageSharp` to 3.x or `SixLabors.ImageSharp.Drawing` to 2.x as part of routine dependency cleanup. Those versions move the dependency graph onto the newer SixLabors license model. A future upgrade should be a deliberate project decision with an explicit license review, not an automatic package bump.

This means `dotnet list package --outdated` is expected to report these packages as outdated. Treat that as an intentional pin unless the license decision changes.

The package references live in `Sources/HtmlTinkerX/HtmlTinkerX.csproj` and apply to every target framework, including `net472`, `net8.0`, and `net10.0`.

## JavaScript parser packages

HtmlTinkerX currently references Jint 4.x for JavaScript execution support. Jint 4.x depends on Acornima, not Esprima. Older Jint 3.x builds used Esprima, so older PowerShell examples that reference types such as `[Esprima.JavaScriptParser]` should be updated to the Acornima surface exposed by the module.

Current PowerShell-friendly entry points:

- `ConvertFrom-JavaScriptAst` parses JavaScript into an Acornima AST.
- `Select-JavaScriptAstNode` traverses descendant AST nodes by type, replacing the common `DescendantNodes` workflow.
- `Select-JavaScriptVariable` finds variable declarations by exact, contains, or starts-with name matches.
- Packaged builds expose selected type accelerators such as `[Acornima.Parser]`, `[Acornima.ParserOptions]`, `[Acornima.Ast.Node]`, `[Acornima.Ast.VariableDeclaration]`, `[Acornima.Ast.ObjectExpression]`, and `[Acornima.Ast.ClassBody]`.

Do not add Esprima back only for compatibility unless the project intentionally decides to carry both parser APIs. Prefer Acornima cmdlets and type accelerators for new work because they match the current Jint dependency graph.
