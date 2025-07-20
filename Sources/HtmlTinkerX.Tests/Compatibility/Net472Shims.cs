using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace HtmlTinkerX.Tests
{
    internal static class Net472Shims
    {
#if FRAMEWORK
        public static class RuntimeHelpersCompat
        {
            public static object GetUninitializedObject(Type type)
            {
                return FormatterServices.GetUninitializedObject(type);
            }
        }

        public static class FileCompat
        {
            public static Task WriteAllTextAsync(string path, string contents)
            {
                return Task.Run(() => File.WriteAllText(path, contents));
            }
        }

        public static class PathCompat
        {
            public static string GetRelativePath(string relativeTo, string path)
            {
                if (!relativeTo.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    relativeTo += Path.DirectorySeparatorChar;

                Uri fromUri = new Uri(relativeTo);
                Uri toUri = new Uri(path);

                Uri relativeUri = fromUri.MakeRelativeUri(toUri);
                string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

                return relativePath.Replace('/', Path.DirectorySeparatorChar);
            }
        }
#endif
    }
}