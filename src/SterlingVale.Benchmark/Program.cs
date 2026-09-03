using SterlingVale.Benchmark;
using SterlingVale.Benchmark.Commands;
using SterlingVale.Benchmark.Model;
using SterlingVale.Benchmark.Reporting;

// Sterling Vale benchmark CLI.
//   benchmark validate-data
//   benchmark run --profile small|medium|large [--trials N]
//   benchmark compare [--trials N]
//   benchmark report --comparison-id <id>

if (args.Length == 0)
{
    PrintUsage();
    return 2;
}

string command = args[0];
var options = ParseOptions(args);

switch (command)
{
    case "validate-data":
        return ValidateData.Run(new Composition().DataRoot);

    case "run":
        return await RunAsync(options.GetValueOrDefault("profile", "medium"), TrialsFrom(options));

    case "compare":
        return await CompareAsync(TrialsFrom(options));

    case "report":
        return Report(options.GetValueOrDefault("comparison-id"));

    default:
        PrintUsage();
        return 2;
}

static async Task<int> RunAsync(string profile, int? trials)
{
    var composition = new Composition();
    if (!composition.Snapshots.AvailableProfiles.Contains(profile, StringComparer.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"Dataset profile '{profile}' is not available under '{composition.DataRoot}'. Run the data generator first.");
        return 1;
    }

    var harness = new BenchmarkHarness(composition);
    var report = await harness.RunAsync(profile, trials ?? composition.Trials, CancellationToken.None);
    string path = ComparisonWriter.Write(report, composition.ArtifactsRoot);
    PrintSummary(report, path, composition.Live);
    return report.Fairness.Matched ? 0 : 1;
}

static async Task<int> CompareAsync(int? trials)
{
    var composition = new Composition();
    var harness = new BenchmarkHarness(composition);
    int exit = 0;
    foreach (var profile in composition.Snapshots.AvailableProfiles)
    {
        var report = await harness.RunAsync(profile, trials ?? composition.Trials, CancellationToken.None);
        string path = ComparisonWriter.Write(report, composition.ArtifactsRoot);
        PrintSummary(report, path, composition.Live);
        if (!report.Fairness.Matched)
        {
            exit = 1;
        }
    }

    return exit;
}

static int Report(string? comparisonId)
{
    if (string.IsNullOrWhiteSpace(comparisonId))
    {
        Console.Error.WriteLine("--comparison-id is required.");
        return 2;
    }

    var composition = new Composition();
    string jsonPath = Path.Combine(composition.ArtifactsRoot, "comparisons", comparisonId + ".json");
    if (!File.Exists(jsonPath))
    {
        Console.Error.WriteLine($"Comparison '{comparisonId}' not found at {jsonPath}.");
        return 1;
    }

    var report = ComparisonWriter.Read(jsonPath);
    string markdown = ComparisonWriter.RenderMarkdown(report);
    string mdPath = Path.Combine(composition.ArtifactsRoot, "comparisons", comparisonId + ".md");
    File.WriteAllText(mdPath, markdown);
    Console.WriteLine($"Wrote {mdPath}");
    return 0;
}

static void PrintSummary(ComparisonReport report, string path, bool live)
{
    Console.WriteLine($"Comparison {report.ComparisonId} ({report.Profile}) — {(live ? "LIVE model" : "offline fake model")}");
    Console.WriteLine($"  fairness: {(report.Fairness.Matched ? "MATCHED" : "FAILED: " + string.Join(",", report.Fairness.Mismatches))}");
    Console.WriteLine($"  classic  duration median {report.Classic.DurationMs.Median:0.###} ms; codeact duration median {report.CodeAct.DurationMs.Median:0.###} ms");
    Console.WriteLine($"  written: {path}");
}

static int? TrialsFrom(IReadOnlyDictionary<string, string> options) =>
    options.TryGetValue("trials", out var raw) && int.TryParse(raw, out var n) ? n : null;

static Dictionary<string, string> ParseOptions(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 1; i < args.Length - 1; i++)
    {
        if (args[i].StartsWith("--", StringComparison.Ordinal))
        {
            options[args[i][2..]] = args[i + 1];
        }
    }

    return options;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  benchmark validate-data");
    Console.WriteLine("  benchmark run --profile small|medium|large [--trials N]");
    Console.WriteLine("  benchmark compare [--trials N]");
    Console.WriteLine("  benchmark report --comparison-id <id>");
}
