using System;
using System.Collections.Generic;
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
        foreach (IGrouping<string, MethodInfo> family in methods.GroupBy(method => method.DeclaringType!.FullName + "|" + method.Name)) {
            MethodInfo[] boundedMethods = family
                .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(HtmlHttpFetchOptions)))
                .ToArray();
            Assert.NotEmpty(boundedMethods);
            foreach (MethodInfo method in boundedMethods) {
                ParameterInfo[] parameters = method.GetParameters();
                Assert.Equal(typeof(CancellationToken), parameters[parameters.Length - 1].ParameterType);
            }
        }
    }

    [Fact]
    public void LegacyUrlFetchSignatures_RemainAvailableForCompiledConsumers() {
        AssertMethod(typeof(HtmlUtilities), nameof(HtmlUtilities.GetStringWithProperEncodingAsync), typeof(HttpClient), typeof(string), typeof(CancellationToken));
        AssertMethod(typeof(HtmlUtilities), nameof(HtmlUtilities.ReadResponseContentWithProperEncodingAsync), typeof(HttpResponseMessage), typeof(CancellationToken));
        AssertMethod(typeof(HtmlFormFieldExtractor), nameof(HtmlFormFieldExtractor.ExtractUrlFieldsAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlJsonLdParser), nameof(HtmlJsonLdParser.ParseUrlAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlAppStateParser), nameof(HtmlAppStateParser.ParseUrlAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlHeadLinkParser), nameof(HtmlHeadLinkParser.ParseUrlAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlTokenParser), nameof(HtmlTokenParser.ParseUrlAsync), typeof(string), typeof(string[]), typeof(HttpClient));
        AssertMethod(typeof(HtmlRobotsParser), nameof(HtmlRobotsParser.ParseUrlAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlOutlineBuilder), nameof(HtmlOutlineBuilder.BuildFromUrlAsync), typeof(string), typeof(HtmlParserEngine), typeof(HttpClient));
        AssertMethod(typeof(HtmlScriptDataParser), nameof(HtmlScriptDataParser.ParseUrlAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlLinkedJavaScriptEndpointParser), nameof(HtmlLinkedJavaScriptEndpointParser.ParseAsync), typeof(string), typeof(Uri), typeof(bool), typeof(HttpClient));
        AssertMethod(typeof(HtmlLinkedJavaScriptEndpointParser), nameof(HtmlLinkedJavaScriptEndpointParser.ParseUrlAsync), typeof(string), typeof(bool), typeof(HttpClient));
        AssertMethod(typeof(HtmlImageCandidateParser), nameof(HtmlImageCandidateParser.ParseUrlAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlWebManifestParser), nameof(HtmlWebManifestParser.ParseUrlAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlWellKnownParser), nameof(HtmlWellKnownParser.ParseUrlAsync), typeof(string), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParser), nameof(HtmlParser.ParseUrlWithAngleSharpAsync), typeof(string), typeof(HttpClient), typeof(CancellationToken));
        AssertMethod(typeof(HtmlParser), nameof(HtmlParser.ParseUrlWithHtmlAgilityPackAsync), typeof(string), typeof(HttpClient), typeof(CancellationToken));
        AssertMethod(typeof(HtmlParser), nameof(HtmlParser.ParseUrlTablesWithAngleSharpAsync), typeof(string), typeof(IDictionary<string, string>), typeof(IDictionary<string, string>), typeof(bool), typeof(HttpClient), typeof(Func<HttpClient>));
        AssertMethod(typeof(HtmlParser), nameof(HtmlParser.ParseUrlTablesWithHtmlAgilityPackAsync), typeof(string), typeof(bool), typeof(IDictionary<string, string>), typeof(IDictionary<string, string>), typeof(bool), typeof(HttpClient), typeof(Func<HttpClient>));
        AssertMethod(typeof(HtmlParser), nameof(HtmlParser.ParseUrlListsWithAngleSharpAsync), typeof(string), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParser), nameof(HtmlParser.ParseUrlListsWithHtmlAgilityPackAsync), typeof(string), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParser), nameof(HtmlParser.ParseUrlListsWithAngleSharpDetailedAsync), typeof(string), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParser), nameof(HtmlParser.ParseUrlListsWithHtmlAgilityPackDetailedAsync), typeof(string), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParser), nameof(HtmlParser.ParseUrlFormsWithAngleSharpAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParser), nameof(HtmlParser.ParseUrlMetaTagsAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParser), nameof(HtmlParser.ParseUrlOpenGraphAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParser), nameof(HtmlParser.ParseUrlMicrodataItemsAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParserFromForm), nameof(HtmlParserFromForm.ParseUrlFormsWithAngleSharpAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParserFromList), nameof(HtmlParserFromList.ParseUrlListsWithAngleSharpDetailedAsync), typeof(string), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParserFromList), nameof(HtmlParserFromList.ParseUrlListsWithAngleSharpAsync), typeof(string), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParserFromList), nameof(HtmlParserFromList.ParseUrlListsWithHtmlAgilityPackDetailedAsync), typeof(string), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParserFromList), nameof(HtmlParserFromList.ParseUrlListsWithHtmlAgilityPackAsync), typeof(string), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParserFromMeta), nameof(HtmlParserFromMeta.ParseUrlMetaTagsAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParserFromMicrodata), nameof(HtmlParserFromMicrodata.ParseUrlMicrodataItemsAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParserFromOpenGraph), nameof(HtmlParserFromOpenGraph.ParseUrlOpenGraphAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlParserFromTable), nameof(HtmlParserFromTable.ParseUrlTablesWithAngleSharpAsync), typeof(string), typeof(IDictionary<string, string>), typeof(IDictionary<string, string>), typeof(bool), typeof(HttpClient), typeof(Func<HttpClient>));
        AssertMethod(typeof(HtmlParserFromTable), nameof(HtmlParserFromTable.ParseUrlTablesWithHtmlAgilityPackAsync), typeof(string), typeof(bool), typeof(IDictionary<string, string>), typeof(IDictionary<string, string>), typeof(bool), typeof(HttpClient), typeof(Func<HttpClient>));
        AssertMethod(typeof(HtmlReactFlightParser), nameof(HtmlReactFlightParser.ParseUrlAsync), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlResourceParser), nameof(HtmlResourceParser.ParseUrlAsync), typeof(string), typeof(bool), typeof(bool), typeof(HttpClient));
        AssertMethod(typeof(HtmlResourceParser), nameof(HtmlResourceParser.DownloadResourcesAsync), typeof(IEnumerable<HtmlResourceLink>), typeof(Uri), typeof(string), typeof(HttpClient));
        AssertMethod(typeof(HtmlResourceParser), nameof(HtmlResourceParser.DownloadResourcesFromUrlAsync), typeof(string), typeof(string), typeof(bool), typeof(HttpClient));
        AssertMethod(typeof(HtmlResourceLink), nameof(HtmlResourceLink.SaveAsync), typeof(string), typeof(Uri), typeof(HttpClient));
    }

    private static void AssertMethod(Type type, string name, params Type[] parameterTypes) {
        MethodInfo? method = type.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        Assert.NotNull(method);
    }

    [Fact]
    public void StaticBoundedUrlFetchApis_PutCancellationLast() {
        MethodInfo[] methods = typeof(HtmlParser).Assembly
            .GetExportedTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => typeof(Task).IsAssignableFrom(method.ReturnType))
            .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(HtmlHttpFetchOptions)))
            .ToArray();

        Assert.NotEmpty(methods);
        foreach (MethodInfo method in methods) {
            ParameterInfo[] parameters = method.GetParameters();
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
        foreach (IGrouping<string, MethodInfo> family in methods.GroupBy(method => method.DeclaringType!.FullName + "|" + method.Name)) {
            MethodInfo[] boundedMethods = family
                .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(HtmlHttpFetchOptions)))
                .ToArray();
            Assert.NotEmpty(boundedMethods);
            foreach (MethodInfo method in boundedMethods) {
            ParameterInfo[] parameters = method.GetParameters();
            Assert.Equal(typeof(CancellationToken), parameters[parameters.Length - 1].ParameterType);
            }
        }
    }
}
