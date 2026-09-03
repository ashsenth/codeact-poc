using Microsoft.Extensions.AI;
using SterlingVale.Application.Portfolio;
using SterlingVale.Domain.Analysis;

namespace SterlingVale.AgentShared.Analysis;

/// <summary>
/// Context passed when creating a chat client. Offline (fake) providers use it to synthesize a
/// deterministic plan; live providers ignore it. <see cref="Mode"/> lets the offline planner target
/// the tool surface each mode exposes (Classic tools directly vs CodeAct <c>execute_code</c>).
/// </summary>
public sealed record ChatClientRequest(PortfolioSnapshot Snapshot, AnalysisRequest Request, string RunId, string Mode);

/// <summary>
/// Supplies the <see cref="IChatClient"/> that drives an agent. A live implementation returns an
/// Azure OpenAI client; the offline implementation returns a deterministic fake so the apps run
/// without model access. The <b>same</b> provider is used by both modes to avoid a benchmark
/// confounder.
/// </summary>
public interface IChatClientProvider
{
    /// <summary>True when this provider talks to a live model.</summary>
    bool IsLive { get; }

    /// <summary>The model id reported to callers.</summary>
    string ModelId { get; }

    /// <summary>Creates a chat client for a run. Caller owns disposal.</summary>
    IChatClient CreateChatClient(ChatClientRequest request);
}
