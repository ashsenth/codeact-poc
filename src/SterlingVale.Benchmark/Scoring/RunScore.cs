namespace SterlingVale.Benchmark.Scoring;

/// <summary>Correctness score of one run against the deterministic oracle.</summary>
public sealed record RunScore(
    bool SchemaValid,
    string Status,
    double FlaggedPrecision,
    double FlaggedRecall,
    double BreachPrecision,
    double BreachRecall,
    decimal AllocationMae,
    decimal TradeNotionalMaeFraction,
    decimal MaxAllocationError,
    string MatchClass)
{
    /// <summary>A score for a run that produced no usable output.</summary>
    public static RunScore ForFailure(string status, bool schemaValid, string matchClass) =>
        new(schemaValid, status, 0d, 0d, 0d, 0d, 0m, 0m, 0m, matchClass);
}
