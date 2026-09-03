namespace SterlingVale.AgentShared.Fakes;

/// <summary>A single scripted function call the fake model should emit.</summary>
public sealed record FakeFunctionCall(string Name, IReadOnlyDictionary<string, object?> Arguments);

/// <summary>
/// One scripted turn of the fake model: either a set of function calls (the agent will execute the
/// matching tools and re-invoke the model) or a final text response.
/// </summary>
public sealed record FakeTurn
{
    /// <summary>Function calls to emit this turn, or null for a text turn.</summary>
    public IReadOnlyList<FakeFunctionCall>? FunctionCalls { get; init; }

    /// <summary>Final text to emit this turn, or null for a function-call turn.</summary>
    public string? Text { get; init; }

    /// <summary>True when this turn emits function calls.</summary>
    public bool IsFunctionCall => FunctionCalls is { Count: > 0 };
}

/// <summary>
/// A deterministic, ordered plan of fake-model turns. The plan is data — it contains no live-model
/// behavior — so tests and offline runs are fully reproducible.
/// </summary>
public sealed class FakeChatPlan
{
    private readonly List<FakeTurn> _turns = [];

    /// <summary>The ordered turns.</summary>
    public IReadOnlyList<FakeTurn> Turns => _turns;

    /// <summary>Adds a turn that calls a single tool.</summary>
    public FakeChatPlan CallTool(string name, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        _turns.Add(new FakeTurn
        {
            FunctionCalls = [new FakeFunctionCall(name, arguments ?? new Dictionary<string, object?>())],
        });
        return this;
    }

    /// <summary>Adds a turn that calls multiple tools in parallel.</summary>
    public FakeChatPlan CallTools(params FakeFunctionCall[] calls)
    {
        ArgumentNullException.ThrowIfNull(calls);
        _turns.Add(new FakeTurn { FunctionCalls = calls });
        return this;
    }

    /// <summary>Adds a final text turn (typically the schema-valid report JSON).</summary>
    public FakeChatPlan Respond(string text)
    {
        _turns.Add(new FakeTurn { Text = text });
        return this;
    }
}
