# Dependency Policy

## Screenshot image processing

HtmlTinkerX uses ChartForgeX for dependency-free screenshot post-processing:

- decoding Playwright PNG/JPEG screenshot bytes for overlays and format conversion
- drawing selector highlight rectangles and overlay text
- encoding PNG, JPEG, BMP, and GIF screenshot output

The package reference lives in `Sources/HtmlTinkerX/HtmlTinkerX.csproj` and applies to every target framework, including `net472`, `net8.0`, and `net10.0`. Local development can pass `-p:ChartForgeXProjectPath=...` to validate against a sibling ChartForgeX checkout before a package is published.

Keep screenshot overlays thin and route reusable raster behavior through ChartForgeX instead of adding a second image-processing stack.

## JavaScript parser packages

HtmlTinkerX currently references Jint 4.x for JavaScript execution support. Jint 4.x depends on Acornima, not Esprima. Older Jint 3.x builds used Esprima, so older PowerShell examples that reference types such as `[Esprima.JavaScriptParser]` should be updated to the Acornima surface exposed by the module.

Current PowerShell-friendly entry points:

- `ConvertFrom-JavaScriptAst` parses JavaScript into an Acornima AST.
- `Select-JavaScriptAstNode` traverses descendant AST nodes by type, replacing the common `DescendantNodes` workflow. Use `-IncludeRoot` for `DescendantNodesAndSelf`-style output.
- `Select-JavaScriptVariable` finds variable declarations and loose assignments by exact, contains, or starts-with name matches. It can match member assignment paths such as `window.$Config` and read dotted object values with `-PropertyPath`.
- `Select-HtmlJavaScriptVariable` applies the same JavaScript variable selection to inline JavaScript script tags in HTML, skipping non-JavaScript scripts such as JSON-LD.

## React Server Component / React Flight payloads

Modern Next.js pages can inline React Flight payload instructions in `<script>` tags that push data into `self.__next_f`. Use `ConvertFrom-HtmlRscPayload` to extract those instructions through stable HtmlTinkerX model objects instead of relying on Acornima or framework implementation types directly.

The cmdlet returns decoded Flight rows by default, `-RawPayload` returns the raw inline payload instructions, and `-AsDocument` returns both collections together. This is a static extractor for server-rendered app state; it does not hydrate React, execute application JavaScript, or resolve client module references.

## Stable parsing surfaces instead of dependency exposure

Prefer workflow cmdlets and HtmlTinkerX model objects over new type accelerators. The module now exposes static parsers for JSON-LD (`ConvertFrom-HtmlJsonLd`), generic script data (`ConvertFrom-HtmlScriptData`), embedded app state (`ConvertFrom-HtmlAppState`), head discovery links (`ConvertFrom-HtmlHeadLink`), image candidates (`ConvertFrom-HtmlImageCandidate`), token extraction (`Select-HtmlToken`), JavaScript endpoint discovery (`ConvertFrom-JavaScriptEndpoint` and `ConvertFrom-HtmlLinkedJavaScriptEndpoint`), web manifests (`ConvertFrom-WebManifest`), well-known text files (`ConvertFrom-WellKnownText`), and robots.txt (`ConvertFrom-RobotsTxt`). These keep common parsing workflows available without requiring users to script directly against bundled dependency types.
- Packaged builds expose public dependency enums plus a small explicit set of practical document/node accelerators such as `[Acornima.Ast.Node]`, `[Acornima.Ast.Program]`, `[Acornima.Ast.Script]`, `[HtmlAgilityPack.HtmlDocument]`, `[HtmlAgilityPack.HtmlNode]`, and `[HtmlAgilityPack.HtmlAttribute]`. New JavaScript AST workflows should prefer cmdlets and HtmlTinkerX helper APIs over adding accelerator entries.

Do not add Esprima back only for compatibility unless the project intentionally decides to carry both parser APIs. Prefer Acornima cmdlets and type accelerators for new work because they match the current Jint dependency graph.
