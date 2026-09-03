using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Fakes;
using SterlingVale.Tools;
using Xunit;

namespace SterlingVale.AgentShared.Tests;

public sealed class AgentLoopTests
{
    private const string FinalJson = "{\"households\":[]}";

    private static AIAgent BuildAgent(FakeChatPlan plan)
    {
        var service = new PortfolioToolService(SnapshotHelper.Build());
        var tools = new ToolCatalog().CreateTools(service);
        var client = new DeterministicFakeChatClient(plan);
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

    private static FakeChatPlan ToolThenReport() =>
        new FakeChatPlan().CallTool("list_households").Respond(FinalJson);

    [Fact]
    public async Task Agent_executes_tool_then_returns_final_text()
    {
        var agent = BuildAgent(ToolThenReport());
        var response = await agent.RunAsync("analyze");

        Assert.Equal(FinalJson, response.Text.Trim());

        bool toolExecuted = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Any();
        Assert.True(toolExecuted, "Expected the fake model's scripted tool call to be executed.");
    }

    [Fact]
    public async Task Run_is_deterministic()
    {
        var first = await BuildAgent(ToolThenReport()).RunAsync("analyze");
        var second = await BuildAgent(ToolThenReport()).RunAsync("analyze");
        Assert.Equal(first.Text.Trim(), second.Text.Trim());
    }

    [Fact]
    public async Task Usage_is_aggregated_across_turns()
    {
        var response = await BuildAgent(ToolThenReport()).RunAsync("analyze");
        Assert.NotNull(response.Usage);
        Assert.True(response.Usage!.TotalTokenCount > 0);
    }
}
