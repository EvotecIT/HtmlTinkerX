using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests;

public class PublicHttpApiContractTests {
    [Fact]
    public void StaticUrlFetchApis_ExposeBoundAndCancellationAtTheEnd() {
        MethodInfo[] methods = typeof(HtmlParser).Assembly
            .GetExportedTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.Name.IndexOf("Url", StringComparison.OrdinalIgnoreCase) >= 0)
            .Where(method => typeof(Task).IsAssignableFrom(method.ReturnType))
            .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(HttpClient)))
            .OrderBy(method => method.DeclaringType!.FullName, StringComparer.Ordinal)
            .ThenBy(method => method.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(methods);
        foreach (MethodInfo method in methods) {
            ParameterInfo[] parameters = method.GetParameters();
            Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(HtmlHttpFetchOptions));
            Assert.Equal(typeof(CancellationToken), parameters[parameters.Length - 1].ParameterType);
        }
    }

    [Fact]
    public void PublicResourceDownloadApis_ExposeBoundAndCancellationAtTheEnd() {
        MethodInfo[] methods = typeof(HtmlParser).Assembly
            .GetExportedTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            .Where(method => typeof(Task).IsAssignableFrom(method.ReturnType))
            .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(HttpClient)))
            .Where(method => method.Name.IndexOf("Download", StringComparison.OrdinalIgnoreCase) >= 0 || method.DeclaringType == typeof(HtmlResourceLink))
            .ToArray();

        Assert.NotEmpty(methods);
        foreach (MethodInfo method in methods) {
            ParameterInfo[] parameters = method.GetParameters();
            Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(HtmlHttpFetchOptions));
            Assert.Equal(typeof(CancellationToken), parameters[parameters.Length - 1].ParameterType);
        }
    }
}
