using SterlingVale.Benchmark;
using SterlingVale.Benchmark.Commands;
using SterlingVale.Benchmark.Model;
using SterlingVale.Benchmark.Reporting;

// Sterling Vale benchmark CLI.
//   benchmark validate-data
//   benchmark run --profile small|medium|large [--trials N] [--open]
//   benchmark compare [--trials N]
//   benchmark report --comparison-id <id> [--open]
//   benchmark visualize [--profiles small,medium,large] [--open]

if (args.Length == 0)
{
    PrintUsage();
    return 2;
}

string command = args[0];
var options = ParseOptions(args);
bool open = args.Contains("--open", StringComparer.OrdinalIgnoreCase);

switch (command)
{
    case "validate-data":
        return ValidateData.Run(new Composition().DataRoot);

    case "run":
        return await RunAsync(options.GetValueOrDefault("profile", "medium"), TrialsFrom(options), open);

    case "compare":
        return await CompareAsync(TrialsFrom(options));

    case "report":
        return Report(options.GetValueOrDefault("comparison-id"), open);

    case "visualize":
        return Visualize(options, open);

    default:
        PrintUsage();
        return 2;
}

static async Task<int> RunAsync(string profile, int? trials, bool open)
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
    if (open)
    {
        OpenInBrowser(Path.ChangeExtension(path, ".html"));
    }

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

static int Report(string? comparisonId, bool open)
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
    string dir = Path.Combine(composition.ArtifactsRoot, "comparisons");
    string mdPath = Path.Combine(dir, comparisonId + ".md");
    string csvPath = Path.Combine(dir, comparisonId + ".csv");
    string htmlPath = Path.Combine(dir, comparisonId + ".html");
    File.WriteAllText(mdPath, ComparisonWriter.RenderMarkdown(report));
    File.WriteAllText(csvPath, ComparisonWriter.RenderCsv(report));
    File.WriteAllText(htmlPath, ComparisonWriter.RenderHtml(report));
    Console.WriteLine($"Wrote {mdPath}");
    Console.WriteLine($"Wrote {csvPath}");
    Console.WriteLine($"Wrote {htmlPath}");
    if (open)
    {
        OpenInBrowser(htmlPath);
    }

    return 0;
}

static void OpenInBrowser(string path)
{
    try
    {
        using var _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }
    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or System.PlatformNotSupportedException)
    {
        Console.Error.WriteLine($"Could not open '{path}' automatically: {ex.Message}");
    }
}

static int Visualize(IReadOnlyDictionary<string, string> options, bool open)
{
    var composition = new Composition();
    string dir = Path.Combine(composition.ArtifactsRoot, "comparisons");
    if (!Directory.Exists(dir))
    {
        Console.Error.WriteLine($"No comparisons directory at {dir}. Run the benchmark first.");
        return 1;
    }

    string[] profiles = options.TryGetValue("profiles", out var raw)
        ? raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : ["small", "medium", "large"];

    var reports = new List<ComparisonReport>();
    foreach (var profile in profiles)
    {
        string? latest = Directory.EnumerateFiles(dir, $"{profile}-*.json")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .FirstOrDefault();
        if (latest is null)
        {
            Console.Error.WriteLine($"No comparison found for profile '{profile}'; skipping.");
            continue;
        }

        reports.Add(ComparisonWriter.Read(latest));
    }

    if (reports.Count == 0)
    {
        Console.Error.WriteLine("No comparisons to visualize. Run the benchmark for at least one profile.");
        return 1;
    }

    string outPath = Path.Combine(dir, $"scaling-{DateTime.UtcNow:yyyyMMddHHmmss}.html");
    File.WriteAllText(outPath, ComparisonWriter.RenderScalingHtml(reports));
    Console.WriteLine($"Wrote {outPath} ({reports.Count} profile(s))");
    if (open)
    {
        OpenInBrowser(outPath);
    }

    return 0;
}

static void PrintSummary(ComparisonReport report, string path, bool live)
{
    Console.WriteLine($"Comparison {report.ComparisonId} ({report.Profile}) — {(live ? "LIVE model" : "offline fake model")}");
    Console.WriteLine($"  fairness: {(report.Fairness.Matched ? "MATCHED" : "FAILED: " + string.Join(",", report.Fairness.Mismatches))}");
    Console.WriteLine($"  classic  duration median {report.Classic.DurationMs.Median:0.###} ms; codeact duration median {report.CodeAct.DurationMs.Median:0.###} ms");
    if (report.Delta is not null)
    {
        Console.WriteLine($"  delta (codeact vs classic): {ComparisonWriter.FormatDeltaSummary(report.Delta)}");
    }

    Console.WriteLine($"  written: {path}");
    Console.WriteLine($"  view:    {Path.ChangeExtension(path, ".html")}");
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
    Console.WriteLine("  benchmark run --profile small|medium|large [--trials N] [--open]");
    Console.WriteLine("  benchmark compare [--trials N]");
    Console.WriteLine("  benchmark report --comparison-id <id> [--open]");
    Console.WriteLine("  benchmark visualize [--profiles small,medium,large] [--open]");
}
