using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Agents;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Fakes;
using SterlingVale.Application.Portfolio;
using SterlingVale.Benchmark;
using SterlingVale.CodeAct;
using SterlingVale.Domain.Analysis;
using SterlingVale.Tools;
using SterlingVale.Telemetry;
using Xunit;

namespace SterlingVale.Comparison.Tests;

public sealed class TurnLimitTests : IDisposable
{
    private const int Budget = 20;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sv-turn-limit-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("classic", false)]
    [InlineData("classic", true)]
    [InlineData("codeact", false)]
    [InlineData("codeact", true)]
    public async Task Loop_is_stopped_before_request_twenty_one_for_measured_runs_and_warmups(string mode, bool warmup)
    {
        var provider = new LoopProvider(finalAtLimit: false);
        var record = await Service(mode, provider).AnalyzeAsync(new AnalysisRequest(), CancellationToken.None, isWarmup: warmup);
        Assert.Equal(Budget, provider.Requests);
        Assert.Equal(RunStatus.Capped, record.Response.Status);
        Assert.Null(record.Response.Report);
        Assert.Equal(Budget, record.Response.Metrics.ModelRequestCount);
        Assert.Equal(Budget, record.Response.Metrics.ModelTurnCount);
        Assert.True(record.Response.Metrics.TelemetryValid, string.Join("; ", record.Response.Metrics.TelemetryErrors));
        Assert.Single(record.Events, e => e.EventType == "TURN_LIMIT_REACHED");
        Assert.DoesNotContain(record.Events, e => e.EventType == "MODEL_REQUEST_FAILED");
        Assert.Equal("TurnLimitReached", BenchmarkHarness.FindFailure(record)!.Category);
        var trial = BenchmarkHarness.AssessWarmup(record, new("oracle", "", []),
            new Dictionary<string, IReadOnlyList<string>>());
        Assert.False(trial.InfrastructureFailed);
        Assert.Equal("Capped", trial.Score.MatchClass);
        Assert.All(record.Events, e => Assert.Equal(warmup, e.IsWarmup));
    }

    [Theory]
    [InlineData("classic")]
    [InlineData("codeact")]
    public async Task Final_answer_on_last_allowed_request_is_not_capped(string mode)
    {
        var provider = new LoopProvider(finalAtLimit: true);
        var record = await Service(mode, provider).AnalyzeAsync(new AnalysisRequest(), CancellationToken.None);
        Assert.Equal(Budget, provider.Requests);
        Assert.Equal(RunStatus.Incomplete, record.Response.Status);
        Assert.True(record.Response.Metrics.SchemaValid);
        Assert.True(record.Response.Metrics.TelemetryValid);
        Assert.DoesNotContain(record.Events, e => e.EventType == "TURN_LIMIT_REACHED");
    }

    private AnalysisService Service(string mode, IChatClientProvider provider)
    {
        IAgentFactory factory = mode == "classic"
            ? new ClassicAgentFactory() : new CodeActAgentFactory(new HyperlightOptions());
        return new(new Snapshots(), provider, factory, new AnalysisRunner(),
            new ModelConfiguration { DeploymentName = "fake-model", MaxTurns = Budget, TimeoutSeconds = 120 },
            new PricingConfiguration(), new ToolMetrics(), new ExecutionOptions { ArtifactsRoot = _root });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class Snapshots : ISnapshotProvider
    {
        public IReadOnlyList<string> AvailableProfiles => ["small"];
        public PortfolioSnapshot GetSnapshot(string profile) => new([], [], [], [], [], []);
        public string GetDatasetHash(string profile) => "test-dataset";
        public DatasetReadiness Validate() => DatasetReadiness.Ready;
    }

    private sealed class LoopProvider(bool finalAtLimit) : IChatClientProvider
    {
        public int Requests { get; private set; }
        public bool IsLive => false;
        public string ModelId => "fake-model";

        public IChatClient CreateChatClient(ChatClientRequest request)
        {
            string tool = request.Mode == "classic" ? "list_households" : "execute_code";
            var arguments = tool == "execute_code" ? new Dictionary<string, object?> { ["code"] = "print('loop')" } : null;
            var plan = new FakeChatPlan().CallTool(tool, arguments);
            for (int i = 1; i < Budget - 1; i++) plan.CallTool(tool, arguments);
            if (finalAtLimit)
                plan.Respond("""{"runId":"r","generatedAt":"2026-01-01T00:00:00Z","status":"incomplete","missingItems":["control"],"households":[]}""");
            else
                for (int i = 0; i < Budget; i++) plan.CallTool(tool, arguments);
            return new CountClient(new DeterministicFakeChatClient(plan), () => Requests++);
        }
    }

    private sealed class CountClient(IChatClient inner, Action count) : DelegatingChatClient(inner)
    {
        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            count();
            return base.GetResponseAsync(messages, options, cancellationToken);
        }
    }
}
