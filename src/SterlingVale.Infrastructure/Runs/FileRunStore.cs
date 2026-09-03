using System.Collections.Concurrent;
using System.Text;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Artifacts;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.Infrastructure.Configuration;

namespace SterlingVale.Infrastructure.Runs;

/// <summary>
/// A run store that persists per-run artifacts to <c>{Root}/runs/{runId}/</c> (result.json,
/// metrics.json, events.jsonl, metadata.json, generated-code.txt) and keeps an in-memory index for
/// fast retrieval. Raw output and generated code are written verbatim, never repaired.
/// </summary>
public sealed class FileRunStore : IRunStore
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly ConcurrentDictionary<string, RunRecord> _index = new(StringComparer.Ordinal);
    private readonly string _runsRoot;
    private readonly ModelConfiguration _model;
    private readonly PricingConfiguration _pricing;

    /// <summary>Creates the store rooted at the configured artifacts directory.</summary>
    public FileRunStore(ArtifactsOptions options, ModelConfiguration model, PricingConfiguration pricing)
    {
        ArgumentNullException.ThrowIfNull(options);
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
        _runsRoot = Path.Combine(options.Root, "runs");
    }

    /// <inheritdoc />
    public void Save(RunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _index[record.Response.RunId] = record;

        string runDir = Path.Combine(_runsRoot, record.Response.RunId);
        Directory.CreateDirectory(runDir);
        foreach (var (fileName, content) in RunArtifacts.Build(record, _model, _pricing))
        {
            File.WriteAllText(Path.Combine(runDir, fileName), content, Utf8);
        }
    }

    /// <inheritdoc />
    public RunRecord? Find(string runId) =>
        _index.TryGetValue(runId, out var record) ? record : null;
}
