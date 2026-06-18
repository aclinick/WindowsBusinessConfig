using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WindowsBusinessConfig.Core;
using WindowsBusinessConfig.Core.Yaml;
using WindowsBusinessConfig.Core.Winget;

namespace WbCfg;

internal static class Program
{
    private const string Usage =
        "wbcfg <command> [args]\n" +
        "\n" +
        "Commands:\n" +
        "  validate <path>    Static-check a .winget document and report issues.\n" +
        "  list <path>        Print resources grouped by category.\n" +
        "  test <path>        Run 'winget configure test' against the document.\n" +
        "  apply <path>       Run 'winget configure' against the document.\n" +
        "  version            Print wbcfg and winget versions.\n";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine(Usage);
            return 1;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "validate" => RunValidate(Tail(args)),
                "list"     => RunList(Tail(args)),
                "test"     => await RunWingetAsync("test",  Tail(args)).ConfigureAwait(false),
                "apply"    => await RunWingetAsync("apply", Tail(args)).ConfigureAwait(false),
                "version"  => await RunVersionAsync().ConfigureAwait(false),
                _          => PrintUsage(),
            };
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static string[] Tail(string[] a) => a.Length <= 1 ? Array.Empty<string>() : a[1..];

    private static int PrintUsage()
    {
        Console.WriteLine(Usage);
        return 1;
    }

    private static int RunValidate(string[] args)
    {
        var path = RequirePath(args);
        var doc = YamlDocumentLoader.Load(path);
        var issues = StaticValidator.Validate(doc);

        Console.WriteLine($"Document: {path}");
        Console.WriteLine($"Resources: {doc.Resources.Count}");
        Console.WriteLine();

        if (issues.Count == 0)
        {
            Console.WriteLine("No issues found.");
            return 0;
        }

        foreach (var issue in issues)
        {
            Console.WriteLine($"[{issue.Severity}] {issue.Message}");
        }

        return issues.Any(i => i.Severity == IssueSeverity.Error) ? 1 : 0;
    }

    private static int RunList(string[] args)
    {
        var path = RequirePath(args);
        var doc = YamlDocumentLoader.Load(path);

        var groups = doc.Resources
            .GroupBy(r => r.Category)
            .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal);

        foreach (var group in groups)
        {
            Console.WriteLine($"== {group.Key} ==");
            foreach (var r in group)
            {
                Console.WriteLine($"  - {r.Name}  [{r.Type}]");
            }
            Console.WriteLine();
        }

        return 0;
    }

    private static async Task<int> RunWingetAsync(string verb, string[] args)
    {
        var path = RequirePath(args);
        var runner = new WingetRunner();
        var result = verb switch
        {
            "test"  => await runner.TestAsync(path).ConfigureAwait(false),
            "apply" => await runner.ApplyAsync(path).ConfigureAwait(false),
            _       => throw new ArgumentException($"Unknown winget verb '{verb}'."),
        };

        if (!string.IsNullOrWhiteSpace(result.StdOut)) Console.Write(result.StdOut);
        if (!string.IsNullOrWhiteSpace(result.StdErr)) Console.Error.Write(result.StdErr);
        return result.ExitCode;
    }

    private static async Task<int> RunVersionAsync()
    {
        Console.WriteLine($"wbcfg {typeof(Program).Assembly.GetName().Version}");
        var winget = WingetRunner.ResolveWinget();
        if (winget is null)
        {
            Console.WriteLine("winget: not found");
            return 0;
        }
        var result = await new WingetRunner(winget).VersionAsync().ConfigureAwait(false);
        Console.WriteLine($"winget: {result.StdOut.Trim()}");
        return 0;
    }

    private static string RequirePath(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("Path argument required.");
        }
        if (!File.Exists(args[0]))
        {
            throw new FileNotFoundException($"Config file not found: {args[0]}");
        }
        return args[0];
    }
}
