using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Prompting;
using SterlingVale.Tools;

namespace SterlingVale.AgentShared.Agents;

/// <summary>
/// Classic orchestration: registers the seven canonical tools DIRECTLY on the agent and lets the
/// model call them one at a time.
/// </summary>
public sealed class ClassicAgentFactory : IAgentFactory
{
    private readonly ToolCatalog _catalog = new();

    /// <inheritdoc />
    public string Mode => "classic";

    /// <inheritdoc />
    public AgentBundle Create(IChatClient chatClient, PortfolioToolService toolService, ModelConfiguration model)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(toolService);
        ArgumentNullException.ThrowIfNull(model);

        var tools = _catalog.CreateTools(toolService).Cast<AITool>().ToList();
        var agent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "sterling-vale-classic",
            ChatOptions = new ChatOptions
            {
                ModelId = model.DeploymentName,
                Instructions = Prompt.Classic,
                Temperature = model.Temperature,
                Tools = tools,
            },
        });

        return new AgentBundle(agent);
    }

    /// <inheritdoc />
    public string ComputeToolFingerprint(PortfolioToolService toolService) =>
        ToolCatalog.ComputeFingerprint(_catalog.CreateTools(toolService));
}
