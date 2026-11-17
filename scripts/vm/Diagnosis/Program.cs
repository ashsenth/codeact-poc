using System.Text.Json;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.Application.Oracle;
using SterlingVale.Benchmark.Model;
using SterlingVale.Benchmark.Reporting;
using SterlingVale.Infrastructure.Data;
using SterlingVale.Diagnostics;

if (args.Length > 0 && args[0] == "analysis-worker")
{
    return await BenchmarkWorkerEntryAsync(args);
}
if (args.Length > 0 && args[0] == "control")
{
    return await FeasibilityControl.RunAsync(args[1..]);
}
if (args.Length > 0 && args[0] == "sandbox-capacity")
{
    return SandboxCapacity.Write(args[1..]);
}

if (args.Length < 3)
    throw new ArgumentException("Usage: oracle <dataset-profile-dir> <output.json> [household IDs...] | sample <comparison.json> <output.html> [pair]");

if (args[0] == "oracle")
{
    var snapshot = new DatasetLoader().Load(args[1]).Snapshot;
    var report = new ExposureOracle().Analyze(snapshot,
        new() { HouseholdIds = args.Length > 3 ? args[3..] : null },
        "oracle", DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
    File.WriteAllText(args[2], JsonSerializer.Serialize(ReportJson.ToDto(report),
        OutputJson.Options));
}
else if (args[0] == "sample")
{
    var original = ComparisonWriter.Read(args[1]);
    int pair = args.Length > 3 ? int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 1;
    var trials = original.RawTrials.Where(trial => trial.PairId == pair).Select(trial => trial with
    {
        RunId = $"sample-{trial.Mode}-1", TrialId = null, CorrelationId = null, ChildRunIds = [],
    }).ToArray();
    if (trials.Length != 2) throw new InvalidDataException("A sample requires one recorded trial per mode.");
    var sample = original with
    {
        ComparisonId = "sample-pair-" + pair, MeasuredTrials = 1, WarmupsPerMode = 0, WarmupTrials = [],
        RawTrials = trials, Classic = Aggregator.Summarize("classic", trials),
        CodeAct = Aggregator.Summarize("codeact", trials), Delta = null,
    };
    string html = ComparisonWriter.RenderHtml(sample).Replace(
        "<h1>Sterling Vale &mdash; Classic vs CodeAct</h1>",
        "<h1>Sanitized sample: one measured pair</h1><p>Derived from captured trials; no new model or guest run. Not the aggregate. Timelines and invocation excerpts are explicitly partial.</p>");
    File.WriteAllText(args[2], html);
}
else
{
    throw new ArgumentException("Expected oracle or sample.");
}
return 0;

static Task<int> BenchmarkWorkerEntryAsync(string[] arguments)
{
    if (arguments.Length != 5 || arguments[1] != "--request" || arguments[3] != "--model" || arguments[4] != "fake")
        throw new ArgumentException("The feasibility worker only supports the offline scripted client.");
    string programPath = Environment.GetEnvironmentVariable("FEASIBILITY_PROGRAM_PATH")
        ?? throw new InvalidOperationException("FEASIBILITY_PROGRAM_PATH is required.");
    return SterlingVale.Benchmark.BenchmarkWorker.ExecuteAsync(arguments[2], "fake",
        new ReferenceProgramClientProvider(File.ReadAllText(programPath)));
}

static class OutputJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
