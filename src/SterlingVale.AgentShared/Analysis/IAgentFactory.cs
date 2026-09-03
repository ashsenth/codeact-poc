using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.Tools;

namespace SterlingVale.AgentShared.Analysis;

/// <summary>
/// Builds the mode-specific <see cref="AIAgent"/> from a chat client and the shared tool service.
/// This is the ONLY place where the two modes intentionally differ (Classic registers tools
/// directly; CodeAct registers them as provider-owned tools). Everything else is shared.
/// </summary>
public interface IAgentFactory
{
    /// <summary>The orchestration mode this factory produces ("classic" or "codeact").</summary>
    string Mode { get; }

    /// <summary>Creates the agent. The returned object owns any mode-specific resources.</summary>
    AgentBundle Create(IChatClient chatClient, PortfolioToolService toolService, ModelConfiguration model);

    /// <summary>Computes the fingerprint of the tool catalog this mode exposes.</summary>
    string ComputeToolFingerprint(PortfolioToolService toolService);
}

/// <summary>An agent plus any disposable resources it owns (e.g. a CodeAct provider).</summary>
public sealed class AgentBundle(AIAgent agent, IDisposable? ownedResource = null) : IDisposable
{
    /// <summary>The constructed agent.</summary>
    public AIAgent Agent { get; } = agent;

    /// <inheritdoc />
    public void Dispose() => ownedResource?.Dispose();
}
