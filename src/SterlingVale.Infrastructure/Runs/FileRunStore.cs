using System.Collections.Concurrent;
using System.Text;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Artifacts;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.Infrastructure.Configuration;

namespace SterlingVale.Infrastructure.Runs;

/// <summary>
/// A run store that persists the full set of per-run artifacts to <c>{Root}/runs/{runId}/</c> (see
/// <see cref="SterlingVale.AgentShared.Artifacts.RunArtifacts"/> for the file set, including the
/// transcript and tool-call logs) and keeps an in-memory index for fast retrieval. Raw output and
/// generated code are written verbatim, never repaired.
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
        foreach (var child in record.ChildRuns)
        {
            Save(child);
        }
        _index[record.Response.RunId] = record;

        string runDir = Path.Combine(_runsRoot, record.Response.RunId);
        Directory.CreateDirectory(runDir);
        foreach (var (fileName, content) in RunArtifacts.Build(record, _model, _pricing))
        {
            string path = Path.Combine(runDir, fileName);
            if (fileName == RunArtifacts.Files.Events && File.Exists(path))
            {
                continue;
            }

            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, content, Utf8);
            File.Move(temporary, path, overwrite: true);
        }
    }

    /// <inheritdoc />
    public RunRecord? Find(string runId) =>
        _index.TryGetValue(runId, out var record) ? record : null;
}
