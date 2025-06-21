using PSParseHTML;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class Program {
    private static readonly Dictionary<string, Func<Task>> Examples = new() {
        ["GetHTMLLoginFormSimple"] = ExampleGetHTMLLoginFormSimple.RunAsync,
        ["GetHTMLLoginFormAdvanced"] = ExampleGetHTMLLoginFormAdvanced.RunAsync,
        ["ShowHtmlHarExample"] = ShowHtmlHarExample.RunAsync
    };

    public static async Task Main(string[] args) {
        IEnumerable<string> toRun = args.Length == 0 ? Examples.Keys : args;
        foreach (string name in toRun) {
            if (Examples.TryGetValue(name, out var run)) {
                Console.WriteLine($"Running {name}...");
                await run();
            } else {
                Console.WriteLine($"Unknown example {name}");
            }
        }
    }
}