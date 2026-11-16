using SterlingVale.Benchmark;
using SterlingVale.Benchmark.Commands;
using SterlingVale.Benchmark.Model;
using SterlingVale.Benchmark.Reporting;
using SterlingVale.Telemetry;
using SterlingVale.Infrastructure.Runs;
using SterlingVale.AgentShared.Configuration;

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
Dictionary<string, string> options;
ModelSelection selection;
try
{
    options = ParseOptions(args);
    selection = ModelSelection.Parse(options.GetValueOrDefault("model"));
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
if (command is "run" or "compare")
{
    Console.WriteLine($"Model selected: {selection.Mode}. Reason: {selection.Reason}.");
}
if (options.TryGetValue("log-level", out var logLevel))
{
    if (!Enum.TryParse<ExecutionLogLevel>(logLevel, true, out var parsedLevel) || !Enum.IsDefined(parsedLevel))
    {
        Console.Error.WriteLine("--log-level must be minimal, normal, verbose, or debug.");
        return 2;
    }

    Environment.SetEnvironmentVariable("EXECUTION_LOG_LEVEL", parsedLevel.ToString());
}

if (options.TryGetValue("payload-capture", out var capture))
{
    if (!Enum.TryParse<PayloadCaptureMode>(capture, true, out var parsedCapture) || !Enum.IsDefined(parsedCapture))
    {
        Console.Error.WriteLine("--payload-capture must be none, metadata, or full.");
        return 2;
    }

    Environment.SetEnvironmentVariable("EXECUTION_PAYLOAD_CAPTURE", parsedCapture.ToString());
}
bool open = args.Contains("--open", StringComparer.OrdinalIgnoreCase);

try
{
switch (command)
{
    case "analysis-worker":
        return await BenchmarkWorker.ExecuteAsync(options["request"], selection.Mode);

    case "validate-data":
        return ValidateData.Run(new Composition().DataRoot);

    case "run":
        return await RunAsync(options.GetValueOrDefault("profile", "medium"), TrialsFrom(options), open, selection.Mode, HouseholdsFrom(options));

    case "compare":
        return await CompareAsync(TrialsFrom(options), selection.Mode, HouseholdsFrom(options));

    case "report":
        return Report(options.GetValueOrDefault("comparison-id"), open);

    case "visualize":
        return Visualize(options, open);

    default:
        PrintUsage();
        return 2;
}
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(SterlingVale.AgentShared.Analysis.FailureInfo.FromException(exception).Message);
    Console.Error.WriteLine("Check the selected model, Azure endpoint/deployment settings and dataset availability; no comparison succeeded.");
    return 1;
}

static async Task<int> RunAsync(string profile, int? trials, bool open, string modelMode, int? households)
{
    using var composition = new Composition(modelMode);
    Console.WriteLine(composition.Model.DescribeOutputSettings());
    if (Environment.GetEnvironmentVariable("BENCHMARK_REQUIRE_LIVE") == "true" && (!composition.Live || !composition.Hyperlight.HasGuest))
    {
        Console.Error.WriteLine("Live comparison blocked: configure Azure OpenAI and HYPERLIGHT_PYTHON_GUEST_PATH on an x64 virtualization-capable host. No offline substitution permitted.");
        return 1;
    }
    if (!composition.Snapshots.AvailableProfiles.Contains(profile, StringComparer.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"Dataset profile '{profile}' is not available under '{composition.DataRoot}'. Run the data generator first.");
        return 1;
    }

    var harness = new BenchmarkHarness(composition);
    var report = await harness.RunAsync(profile, trials ?? composition.Trials, CancellationToken.None, households);
    string path = ComparisonWriter.Write(report, composition.ArtifactsRoot);
    PrintSummary(report, path, composition.Live);
    if (open)
    {
        OpenInBrowser(Path.ChangeExtension(path, ".html"));
    }

    return report.Outcome.IsValid ? 0 : 1;
}

static async Task<int> CompareAsync(int? trials, string modelMode, int? households)
{
    using var composition = new Composition(modelMode);
    Console.WriteLine(composition.Model.DescribeOutputSettings());
    if (Environment.GetEnvironmentVariable("BENCHMARK_REQUIRE_LIVE") == "true" && (!composition.Live || !composition.Hyperlight.HasGuest))
    {
        Console.Error.WriteLine("Live comparison blocked: Azure OpenAI and a real Hyperlight guest are required. No offline substitution permitted.");
        return 1;
    }
    var harness = new BenchmarkHarness(composition);
    foreach (var profile in composition.Snapshots.AvailableProfiles)
    {
        var report = await harness.RunAsync(profile, trials ?? composition.Trials, CancellationToken.None, households);
        string path = ComparisonWriter.Write(report, composition.ArtifactsRoot);
        PrintSummary(report, path, composition.Live);
        if (!report.Outcome.IsValid)
        {
            return 1;
        }
    }

    return 0;
}

static int Report(string? comparisonId, bool open)
{
    if (string.IsNullOrWhiteSpace(comparisonId))
    {
        Console.Error.WriteLine("--comparison-id is required.");
        return 2;
    }

    using var composition = new Composition();
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
    ArtifactIndex.Write(composition.ArtifactsRoot);
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
    using var composition = new Composition();
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
    var available = Directory.EnumerateFiles(dir, "*.json").Select(ComparisonWriter.Read)
        .OrderByDescending(report => report.GeneratedAtUtc, StringComparer.Ordinal).ToArray();
    var cohort = available.FirstOrDefault(report => profiles.Contains(report.Profile));
    string? promptHash = cohort?.PromptHash;
    string? modelFingerprint = cohort?.ModelFingerprint;
    if (promptHash is null || modelFingerprint is null)
    {
        Console.Error.WriteLine("No comparison with recorded shared-prompt and model-configuration fingerprints. Run the benchmark first.");
        return 1;
    }
    Console.WriteLine($"Selecting latest comparisons only for shared prompt hash {promptHash} and model configuration {modelFingerprint}; other configurations are excluded.");
    foreach (var profile in profiles)
    {
        var latest = available.FirstOrDefault(report => report.Profile == profile && report.PromptHash == promptHash &&
            report.ModelFingerprint == modelFingerprint);
        if (latest is null)
        {
            Console.Error.WriteLine($"No comparison found for profile '{profile}' with selected prompt/model configuration; skipping.");
            continue;
        }

        reports.Add(latest);
    }

    if (reports.Count == 0)
    {
        Console.Error.WriteLine("No comparisons to visualize. Run the benchmark for at least one profile.");
        return 1;
    }

    string outPath = Path.Combine(dir, $"scaling-{DateTime.UtcNow:yyyyMMddHHmmss}.html");
    File.WriteAllText(outPath, ComparisonWriter.RenderScalingHtml(reports));
    ArtifactIndex.Write(composition.ArtifactsRoot);
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
    Console.Write(ComparisonWriter.RenderConsoleSummary(report));
    if (!live)
    {
        Console.WriteLine("  FAKE-MODEL CONTROL: synthetic tokens and formula cost; NOT live performance. The no-guest stand-in executes no Python.");
    }
    if (report.ClassicBatchSize > 0)
    {
        Console.WriteLine($"  classic batched: {report.ClassicBatchSize} household(s) per model invocation (metrics summed across batches)");
    }

    if (report.Outcome.IsValid && report.ExecutionKind == "live-model-hyperlight" && report.Delta is not null)
    {
        Console.WriteLine($"  delta (codeact vs classic): {ComparisonWriter.FormatDeltaSummary(report)}");
    }

    Console.WriteLine($"  written: {path}");
    Console.WriteLine($"  view:    {Path.ChangeExtension(path, ".html")}");
}

static int? TrialsFrom(IReadOnlyDictionary<string, string> options) =>
    !options.TryGetValue("trials", out var raw) ? null :
    int.TryParse(raw, out var n) && n > 0 ? n : throw new InvalidOperationException("--trials must be a positive integer.");

static int? HouseholdsFrom(IReadOnlyDictionary<string, string> options) =>
    !options.TryGetValue("households", out var raw) ? null :
    int.TryParse(raw, out var n) && n > 0 ? n : throw new InvalidOperationException("--households must be a positive integer.");

static Dictionary<string, string> ParseOptions(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--open") continue;
        if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Missing or invalid option value for {args[i]}.");
        }
        options[args[i][2..]] = args[++i];
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
    Console.WriteLine("  execution options: --log-level minimal|normal|verbose|debug --payload-capture none|metadata|full");
    Console.WriteLine("  model selection: --model fake|live (default fake; environment never opts in)");
    Console.WriteLine("  scope: --households N (first N dataset households, same scope and oracle for both modes)");
}
