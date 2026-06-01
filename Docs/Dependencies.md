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

## React Server Component / React Flight payloads

Modern Next.js pages can inline React Flight payload instructions in `<script>` tags that push data into `self.__next_f`. Use `ConvertFrom-HtmlRscPayload` to extract those instructions through stable HtmlTinkerX model objects instead of relying on Acornima or framework implementation types directly.

The cmdlet returns decoded Flight rows by default, `-RawPayload` returns the raw inline payload instructions, and `-AsDocument` returns both collections together. This is a static extractor for server-rendered app state; it does not hydrate React, execute application JavaScript, or resolve client module references.

## Stable parsing surfaces instead of dependency exposure

Prefer workflow cmdlets and HtmlTinkerX model objects over new type accelerators. The module now exposes static parsers for JSON-LD (`ConvertFrom-HtmlJsonLd`), generic script data (`ConvertFrom-HtmlScriptData`), embedded app state (`ConvertFrom-HtmlAppState`), head discovery links (`ConvertFrom-HtmlHeadLink`), image candidates (`ConvertFrom-HtmlImageCandidate`), token extraction (`Select-HtmlToken`), JavaScript endpoint discovery (`ConvertFrom-JavaScriptEndpoint` and `ConvertFrom-HtmlLinkedJavaScriptEndpoint`), web manifests (`ConvertFrom-WebManifest`), well-known text files (`ConvertFrom-WellKnownText`), and robots.txt (`ConvertFrom-RobotsTxt`). These keep common parsing workflows available without requiring users to script directly against bundled dependency types.
- Packaged builds expose selected type accelerators such as `[Acornima.Parser]`, `[Acornima.ParserOptions]`, `[Acornima.Ast.Node]`, `[Acornima.Ast.VariableDeclaration]`, `[Acornima.Ast.ObjectExpression]`, and `[Acornima.Ast.ClassBody]`.

Do not add Esprima back only for compatibility unless the project intentionally decides to carry both parser APIs. Prefer Acornima cmdlets and type accelerators for new work because they match the current Jint dependency graph.
