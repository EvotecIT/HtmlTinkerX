using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace HtmlTinkerX.Tests;

public class AssemblyLoadContextTests {
    [Fact]
    public void OnImportAddsResolver() {
        // invoke the private resolver to ensure it loads assemblies into a custom ALC
#if NET5_0_OR_GREATER
        string assemblyDir = Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location)!;
        string depPath = Path.Combine(assemblyDir, "HtmlAgilityPack.dll");
        Assert.True(File.Exists(depPath));
        AssemblyName name = AssemblyName.GetAssemblyName(depPath);
        MethodInfo method = typeof(OnModuleImportAndRemove).GetMethod("ResolveAlc", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assembly? asm = (Assembly?)method.Invoke(null, new object[] { AssemblyLoadContext.Default, name });
        Assert.NotNull(asm);
        AssemblyLoadContext alc = AssemblyLoadContext.GetLoadContext(asm!)!;
        Assert.NotEqual("Default", alc.Name);
#endif
    }
}