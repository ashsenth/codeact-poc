using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Fakes;
using SterlingVale.Domain.Analysis;
using SterlingVale.Tools;
using Xunit;

namespace SterlingVale.AgentShared.Tests;

/// <summary>
/// Negative-path coverage for <see cref="AnalysisRunner"/>: the runner must surface invalid model
/// output, a per-run timeout, and a turn-cap as distinct failure statuses, and must never invent
/// token usage or cost when the model reports none. These guarantees underpin benchmark fairness.
/// </summary>
public sealed class AnalysisRunnerTests
{
    private const string FinalJson = "{\"households\":[]}";

    private static readonly PricingConfiguration Pricing = new()
    {
        InputPerMillion = 2.50m,
        OutputPerMillion = 10.00m,
    };

    private static ModelConfiguration Model(int maxTurns = 12, int timeoutSeconds = 120) => new()
    {
        DeploymentName = "fake-model",
        Temperature = 0f,
        MaxTurns = maxTurns,
        TimeoutSeconds = timeoutSeconds,
    };

    private static RunContext Context(ModelConfiguration model) => new()
    {
        RunId = "run-under-test",
        Mode = "classic",
        UserMessage = "analyze",
        Model = model,
        Pricing = Pricing,
        Fingerprints = new FairnessFingerprints
        {
            PromptHash = "p",
            SchemaHash = "s",
            ToolFingerprint = "t",
            ModelFingerprint = "m",
            PricingFingerprint = "pr",
            DatasetHash = "d",
        },
    };

    private static AIAgent BuildAgent(IChatClient client)
    {
        var service = new PortfolioToolService(SnapshotHelper.Build());
        var tools = new ToolCatalog().CreateTools(service);
        return client.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "test-agent",
            ChatOptions = new ChatOptions
            {
                Instructions = "Analyze the portfolio.",
                Tools = tools.Cast<AITool>().ToList(),
            },
        });
    }

    [Fact]
    public async Task Invalid_model_output_is_marked_Failed_and_not_repaired()
    {
        var agent = BuildAgent(new DeterministicFakeChatClient(new FakeChatPlan().Respond("this is not json")));

        var record = await new AnalysisRunner().RunAsync(agent, Context(Model()), CancellationToken.None);

        Assert.Equal(RunStatus.Failed, record.Response.Status);
        Assert.Null(record.Response.Report);
        Assert.False(record.Response.Metrics.SchemaValid);
    }

    [Fact]
    public async Task Per_run_timeout_is_marked_TimedOut()
    {
        var agent = BuildAgent(new DelayingChatClient());

        var record = await new AnalysisRunner().RunAsync(
            agent, Context(Model(timeoutSeconds: 0)), CancellationToken.None);

        Assert.Equal(RunStatus.TimedOut, record.Response.Status);
        Assert.Null(record.Response.Report);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_instead_of_timing_out()
    {
        var agent = BuildAgent(new DelayingChatClient());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new AnalysisRunner().RunAsync(agent, Context(Model()), cts.Token));
    }

    [Fact]
    public async Task Exceeding_turn_cap_is_marked_Capped()
    {
        var plan = new FakeChatPlan().CallTool("list_households").Respond(FinalJson);
        var agent = BuildAgent(new DeterministicFakeChatClient(plan));

        var record = await new AnalysisRunner().RunAsync(agent, Context(Model(maxTurns: 1)), CancellationToken.None);

        Assert.Equal(RunStatus.Capped, record.Response.Status);
        Assert.Null(record.Response.Report);
    }

    [Fact]
    public async Task Missing_usage_yields_null_tokens_and_null_cost()
    {
        var plan = new FakeChatPlan().CallTool("list_households").Respond(FinalJson);
        var agent = BuildAgent(new DeterministicFakeChatClient(plan, reportUsage: false));

        var record = await new AnalysisRunner().RunAsync(agent, Context(Model()), CancellationToken.None);

        Assert.Null(record.Response.Metrics.TotalTokens);
        Assert.Null(record.Response.Metrics.InputTokens);
        Assert.Null(record.Response.Metrics.OutputTokens);
        Assert.Null(record.Response.Metrics.EstimatedCostUsd);
    }

    /// <summary>A chat client that never returns until its cancellation token fires.</summary>
    private sealed class DelayingChatClient : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "unreachable"));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            foreach (var update in response.ToChatResponseUpdates())
            {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
            // No unmanaged resources.
        }
    }
}
