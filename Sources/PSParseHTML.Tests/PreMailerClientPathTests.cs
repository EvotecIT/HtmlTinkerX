using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace PSParseHTML.Tests;

public class PreMailerClientPathTests
{
    private static string InvokeNormalize(Uri uri)
    {
        var method = typeof(PreMailerClient).GetMethod("NormalizeFileUriPath", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object[] { uri })!;
    }

    [Fact]
    public void NormalizeFileUriPath_UncPaths()
    {
        Uri uri = new("file:////server/share/test.css");
        string normalized = InvokeNormalize(uri);
        if (Path.DirectorySeparatorChar == '/')
        {
            Assert.Equal("/server/share/test.css", normalized);
        }
        else
        {
            Assert.Equal(@"\\server\share\test.css", normalized);
        }
    }

    [Fact]
    public void NormalizeFileUriPath_LocalPaths()
    {
        Uri uri = new("file:///tmp/test.css");
        string normalized = InvokeNormalize(uri);
        if (Path.DirectorySeparatorChar == '/')
        {
            Assert.Equal("/tmp/test.css", normalized);
        }
        else
        {
            Assert.Equal(@"\tmp\test.css", normalized);
        }
    }

    [Fact]
    public void NormalizeFileUriPath_UncPaths_TrailingSlash()
    {
        Uri uri = new("file:////server/share/folder/");
        string normalized = InvokeNormalize(uri);
        if (Path.DirectorySeparatorChar == '/')
        {
            Assert.Equal("/server/share/folder/", normalized);
        }
        else
        {
            Assert.Equal(@"\\server\share\folder\", normalized);
        }
    }

    [Fact]
    public void NormalizeFileUriPath_LocalPaths_TrailingSlash()
    {
        Uri uri = new("file:///tmp/folder/");
        string normalized = InvokeNormalize(uri);
        if (Path.DirectorySeparatorChar == '/')
        {
            Assert.Equal("/tmp/folder/", normalized);
        }
        else
        {
            Assert.Equal(@"\tmp\folder\", normalized);
        }
    }
}
