global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading.Tasks;
global using Xunit;

#if FRAMEWORK
global using RuntimeHelpers = HtmlTinkerX.Tests.Net472Shims.RuntimeHelpersCompat;
global using static HtmlTinkerX.Tests.Net472Shims.FileCompat;
global using static HtmlTinkerX.Tests.Net472Shims.PathCompat;
#else
global using System.Runtime.CompilerServices;
#endif