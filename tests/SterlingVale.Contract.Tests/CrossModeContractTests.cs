using SterlingVale.AgentShared;
using SterlingVale.AgentShared.Agents;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.AgentShared.Prompting;
using SterlingVale.CodeAct;
using SterlingVale.Tools;
using Xunit;

namespace SterlingVale.Contract.Tests;

/// <summary>
/// Cross-mode contract tests: the Classic and CodeAct orchestrations must expose an identical tool
/// contract, share the same prompt body, and produce matching fairness fingerprints. Divergence
/// here would confound the benchmark.
/// </summary>
public sealed class CrossModeContractTests
{
    private static readonly ModelConfiguration Model = new()
    {
        DeploymentName = "fake-model",
        Temperature = 0f,
        MaxTurns = 12,
        TimeoutSeconds = 120,
    };

    private static readonly PricingConfiguration Pricing = new()
    {
        InputPerMillion = 2.50m,
        OutputPerMillion = 10.00m,
    };

    [Fact]
    public void Both_modes_expose_an_identical_tool_fingerprint()
    {
        var service = new PortfolioToolService(SnapshotHelper.Build());
        var classic = new ClassicAgentFactory();
        var codeact = new CodeActAgentFactory(new HyperlightOptions());

        Assert.Equal(classic.ComputeToolFingerprint(service), codeact.ComputeToolFingerprint(service));
    }

    [Fact]
    public void Both_modes_report_the_same_mode_identifiers()
    {
        Assert.Equal("classic", new ClassicAgentFactory().Mode);
        Assert.Equal("codeact", new CodeActAgentFactory(new HyperlightOptions()).Mode);
    }

    [Fact]
    public void Both_modes_share_the_same_prompt_body()
    {
        Assert.StartsWith(Prompt.Shared, Prompt.Classic, StringComparison.Ordinal);
        Assert.StartsWith(Prompt.Shared, Prompt.CodeAct, StringComparison.Ordinal);
        Assert.Equal(Hashing.Sha256Hex(Prompt.Shared), Hashing.Sha256Hex(Prompt.Shared));
    }

    [Fact]
    public void Fairness_fingerprints_match_across_modes_for_identical_inputs()
    {
        var service = new PortfolioToolService(SnapshotHelper.Build());
        const string datasetHash = "dataset-hash";

        var classic = FairnessFingerprints.Create(
            new ClassicAgentFactory().ComputeToolFingerprint(service), datasetHash, Model, Pricing);
        var codeact = FairnessFingerprints.Create(
            new CodeActAgentFactory(new HyperlightOptions()).ComputeToolFingerprint(service), datasetHash, Model, Pricing);

        Assert.Equal(classic.ToDictionary(), codeact.ToDictionary());
    }

    [Fact]
    public void Schemas_have_stable_versioned_identifiers()
    {
        Assert.Equal("exposure-report/v1", OutputSchema.SchemaId);
        Assert.Equal("analysis-request/v1", ApiSchemas.RequestSchemaId);
        Assert.Equal("analysis-response/v1", ApiSchemas.ResponseSchemaId);
        Assert.Equal("exposure-analysis/v1", Prompt.PromptId);
    }
}
