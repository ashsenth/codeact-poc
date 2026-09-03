namespace SterlingVale.AgentShared.Analysis;

/// <summary>Stores and retrieves completed run records (response, raw output, fingerprints).</summary>
public interface IRunStore
{
    /// <summary>Persists a run record.</summary>
    void Save(RunRecord record);

    /// <summary>Retrieves a run record by id, or null when unknown.</summary>
    RunRecord? Find(string runId);
}
