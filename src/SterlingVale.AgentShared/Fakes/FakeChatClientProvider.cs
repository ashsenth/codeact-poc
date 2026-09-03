using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;

namespace SterlingVale.AgentShared.Fakes;

/// <summary>
/// The default offline <see cref="IChatClientProvider"/>. Produces a deterministic fake client whose
/// scripted answer is the oracle report for the requested snapshot, so both APIs run and benchmark
/// end-to-end without any live model access.
/// </summary>
public sealed class FakeChatClientProvider : IChatClientProvider
{
    /// <inheritdoc />
    public bool IsLive => false;

    /// <inheritdoc />
    public string ModelId => "fake-model";

    /// <inheritdoc />
    public IChatClient CreateChatClient(ChatClientRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = FakeAnalysisPlanner.BuildPlan(request.Snapshot, request.Request, request.RunId, request.Mode);
        return new DeterministicFakeChatClient(plan, ModelId);
    }
}
