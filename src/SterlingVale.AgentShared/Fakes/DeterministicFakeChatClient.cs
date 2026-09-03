using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace SterlingVale.AgentShared.Fakes;

/// <summary>
/// A deterministic, offline <see cref="IChatClient"/> that replays a fixed <see cref="FakeChatPlan"/>.
/// It selects the turn to emit by counting assistant messages already present in the conversation,
/// so it is stateless, thread-safe, and reproducible across retries, warm-ups, and parallel trials.
/// It reports deterministic token usage (or none, to exercise the "usage unknown" path), enabling
/// both APIs to build, run, and be benchmarked in CI without any live model access.
/// </summary>
public sealed class DeterministicFakeChatClient : IChatClient
{
    private readonly IReadOnlyList<FakeTurn> _plan;
    private readonly bool _reportUsage;

    /// <summary>Creates a fake client that replays <paramref name="plan"/>.</summary>
    public DeterministicFakeChatClient(FakeChatPlan plan, string modelId = "fake-model", bool reportUsage = true)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Turns.Count == 0)
        {
            throw new ArgumentException("Fake chat plan must contain at least one turn.", nameof(plan));
        }

        _plan = plan.Turns;
        ModelId = modelId;
        _reportUsage = reportUsage;
    }

    /// <summary>The model id reported on every response.</summary>
    public string ModelId { get; }

    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();

        var history = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        int completedTurns = history.Count(m => m.Role == ChatRole.Assistant);
        int turnIndex = Math.Min(completedTurns, _plan.Count - 1);
        var turn = _plan[turnIndex];

        var message = BuildMessage(turn, turnIndex);
        var response = new ChatResponse(message)
        {
            ModelId = ModelId,
            ResponseId = $"fake-turn-{turnIndex}",
            FinishReason = turn.IsFunctionCall ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
        };

        if (_reportUsage)
        {
            response.Usage = BuildUsage(history, turn);
        }

        return Task.FromResult(response);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // No unmanaged resources.
    }

    private static ChatMessage BuildMessage(FakeTurn turn, int turnIndex)
    {
        if (turn.IsFunctionCall)
        {
            var contents = turn.FunctionCalls!
                .Select(c => (AIContent)new FunctionCallContent(
                    $"{c.Name}-{turnIndex}", c.Name, new Dictionary<string, object?>(c.Arguments)))
                .ToList();
            return new ChatMessage(ChatRole.Assistant, contents);
        }

        return new ChatMessage(ChatRole.Assistant, turn.Text ?? string.Empty);
    }

    private static UsageDetails BuildUsage(IReadOnlyList<ChatMessage> history, FakeTurn turn)
    {
        long input = history.Sum(m => EstimateTokens(m.Text));
        long output = turn.IsFunctionCall ? 4L * turn.FunctionCalls!.Count : EstimateTokens(turn.Text);
        return new UsageDetails
        {
            InputTokenCount = input,
            OutputTokenCount = output,
            TotalTokenCount = input + output,
        };
    }

    private static long EstimateTokens(string? text) =>
        string.IsNullOrEmpty(text) ? 1L : (text.Length + 3) / 4;
}
