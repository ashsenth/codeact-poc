using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Fakes;
using Xunit;

namespace SterlingVale.AgentShared.Tests;

public sealed class DeterministicFakeChatClientTests
{
    private static readonly ChatMessage User = new(ChatRole.User, "analyze");

    private static FakeChatPlan Plan() =>
        new FakeChatPlan().CallTool("list_households").Respond("{\"households\":[]}");

    [Fact]
    public async Task First_turn_emits_the_scripted_function_call()
    {
        using var client = new DeterministicFakeChatClient(Plan());
        var response = await client.GetResponseAsync([User]);

        var call = response.Messages[0].Contents.OfType<FunctionCallContent>().Single();
        Assert.Equal("list_households", call.Name);
        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
    }

    [Fact]
    public async Task Subsequent_turn_emits_final_text()
    {
        using var client = new DeterministicFakeChatClient(Plan());
        // Simulate the agent having already produced one assistant (function-call) turn.
        var history = new List<ChatMessage>
        {
            User,
            new(ChatRole.Assistant, [new FunctionCallContent("list_households-0", "list_households", null)]),
            new(ChatRole.Tool, [new FunctionResultContent("list_households-0", "[]")]),
        };

        var response = await client.GetResponseAsync(history);
        Assert.Equal("{\"households\":[]}", response.Messages[0].Text);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
    }

    [Fact]
    public async Task Usage_is_deterministic()
    {
        using var client = new DeterministicFakeChatClient(Plan());
        var first = (await client.GetResponseAsync([User])).Usage;
        var second = (await client.GetResponseAsync([User])).Usage;

        Assert.NotNull(first);
        Assert.Equal(first!.TotalTokenCount, second!.TotalTokenCount);
    }

    [Fact]
    public async Task Usage_is_unknown_when_disabled()
    {
        using var client = new DeterministicFakeChatClient(Plan(), reportUsage: false);
        var response = await client.GetResponseAsync([User]);
        Assert.Null(response.Usage);
    }

    [Fact]
    public void Empty_plan_is_rejected() =>
        Assert.Throws<ArgumentException>(() => new DeterministicFakeChatClient(new FakeChatPlan()));

    [Fact]
    public async Task Cancellation_is_honored()
    {
        using var client = new DeterministicFakeChatClient(Plan());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.GetResponseAsync([User], cancellationToken: cts.Token));
    }
}

public sealed class HashingTests
{
    [Fact]
    public void Same_input_produces_same_hash() =>
        Assert.Equal(Hashing.Sha256Hex("hello"), Hashing.Sha256Hex("hello"));

    [Fact]
    public void Different_input_produces_different_hash() =>
        Assert.NotEqual(Hashing.Sha256Hex("a"), Hashing.Sha256Hex("b"));

    [Fact]
    public void Line_endings_are_normalized()
    {
        Assert.Equal(Hashing.Sha256Hex("a\r\nb"), Hashing.Sha256Hex("a\nb"));
    }
}
