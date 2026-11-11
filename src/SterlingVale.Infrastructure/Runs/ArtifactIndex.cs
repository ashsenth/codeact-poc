using System.Text;
using System.Text.Json;
using SterlingVale.Domain.Analysis;

namespace SterlingVale.Infrastructure.Runs;

public static class ArtifactIndex
{
    private static readonly object Gate = new();

    public static void Write(string artifactsRoot)
    {
        lock (Gate) WriteCore(artifactsRoot);
    }

    private static void WriteCore(string artifactsRoot)
    {
        var text = new StringBuilder("# Execution artifacts\n\n## Comparisons\n\n");
        string comparisons = Path.Combine(artifactsRoot, "comparisons");
        if (Directory.Exists(comparisons))
        {
            foreach (string file in Directory.EnumerateFiles(comparisons, "*.json").Order(StringComparer.Ordinal))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                var report = document.RootElement;
                string id = report.GetProperty("comparisonId").GetString()!;
                string message = report.TryGetProperty("outcome", out var outcome)
                    ? $"{outcome.GetProperty("message").GetString()}; completion {outcome.GetProperty("completedRuns")}/{outcome.GetProperty("totalRuns")}; oracle correct {outcome.GetProperty("correctRuns")}/{outcome.GetProperty("totalRuns")}"
                    : "historical report; completion/correctness not re-evaluated";
                string kind = report.TryGetProperty("executionKind", out var executionKind) ? executionKind.GetString() ?? "unknown" : "unknown";
                text.AppendLine($"- [{id}]({Link(Path.GetRelativePath(artifactsRoot, Path.ChangeExtension(file, ".html")))}) - {message}; {kind}");
            }
        }
        text.Append("\n## Runs (warm-ups are not measured)\n\n");
        string runs = Path.Combine(artifactsRoot, "runs");
        if (Directory.Exists(runs))
        {
            foreach (string directory in Directory.EnumerateDirectories(runs).Order(StringComparer.Ordinal))
            {
                string metadata = Path.Combine(directory, "metadata.json");
                string status = "incomplete";
                string phase = "measured";
                string mode = "unknown";
                if (File.Exists(metadata))
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(metadata));
                    var root = document.RootElement;
                    var context = root.TryGetProperty("execution", out var execution) && execution.ValueKind == JsonValueKind.Object ? execution : root;
                    if (context.TryGetProperty("isWarmup", out var warmup) && warmup.GetBoolean())
                    {
                        phase = "warm-up (not measured)";
                    }
                    if (context.TryGetProperty("agentMode", out var agentMode))
                        mode = agentMode.GetString() ?? "unknown";
                    string resultFile = Path.Combine(directory, "result.json");
                    if (File.Exists(resultFile))
                    {
                        using var result = JsonDocument.Parse(File.ReadAllText(resultFile));
                        var response = result.RootElement;
                        if (response.TryGetProperty("mode", out var resultMode)) mode = resultMode.GetString() ?? "unknown";
                        if (response.TryGetProperty("status", out var resultStatus) || response.TryGetProperty("Status", out resultStatus))
                            status = resultStatus.ValueKind == JsonValueKind.Number
                                ? Enum.GetName(typeof(RunStatus), resultStatus.GetInt32()) ?? "unknown"
                                : resultStatus.GetString() ?? "unknown";
                    }
                }
                text.Append($"- {Path.GetFileName(directory)} ({mode}, {phase}): **{status}**");
                AddLink(text, artifactsRoot, directory, "timeline.md", "timeline");
                AddLink(text, artifactsRoot, directory, Path.Combine("tools", "invocations.jsonl"), "tool invocations");
                string code = Path.Combine(directory, "codeact");
                var programs = Directory.Exists(code) ? Directory.GetFiles(code, "generated-code-*") : [];
                foreach (string program in programs.Order(StringComparer.Ordinal))
                    text.Append($" | [{Path.GetFileName(program)}]({Link(Path.GetRelativePath(artifactsRoot, program))})");
                if (programs.Length == 0) text.Append(" | generated code: unavailable");
                text.AppendLine();
            }
        }
        Directory.CreateDirectory(artifactsRoot);
        string destination = Path.Combine(artifactsRoot, "index.md");
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, text.ToString(), new UTF8Encoding(false));
        File.Move(temporary, destination, overwrite: true);
    }

    private static void AddLink(StringBuilder text, string root, string directory, string relative, string label)
    {
        string file = Path.Combine(directory, relative);
        text.Append(File.Exists(file) ? $" | [{label}]({Link(Path.GetRelativePath(root, file))})" : $" | {label}: unavailable");
    }

    private static string Link(string path) => string.Join("/", path.Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));
}
