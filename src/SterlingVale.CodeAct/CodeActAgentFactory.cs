using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hyperlight;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Fakes;
using SterlingVale.AgentShared.Prompting;
using SterlingVale.Tools;

namespace SterlingVale.CodeAct;

/// <summary>
/// CodeAct orchestration: exposes the same seven canonical tools as PROVIDER-OWNED tools through
/// <see cref="HyperlightCodeActProvider"/>, reachable inside generated code via <c>call_tool(...)</c>.
/// The model-facing surface is <c>execute_code</c>.
///
/// <para>Security: generated code runs only inside the Hyperlight micro-VM (never the host); outbound
/// network is denied (no allowed domains); no host filesystem is mounted; the provider is disposed
/// after each run. When no Hyperlight guest is configured, an offline <c>execute_code</c> stand-in is
/// used so the API still runs deterministically with the fake model.</para>
///
/// <para>The tool fingerprint is computed over the SAME seven tools as Classic, proving both modes
/// expose identical tools and schemas.</para>
/// </summary>
public sealed class CodeActAgentFactory : IAgentFactory
{
    private readonly ToolCatalog _catalog = new();
    private readonly HyperlightOptions _hyperlight;

    /// <summary>Creates the factory with the configured Hyperlight guest options.</summary>
    public CodeActAgentFactory(HyperlightOptions hyperlight)
    {
        _hyperlight = hyperlight ?? throw new ArgumentNullException(nameof(hyperlight));
    }

    /// <inheritdoc />
    public string Mode => "codeact";

    /// <inheritdoc />
    public AgentBundle Create(IChatClient chatClient, PortfolioToolService toolService, ModelConfiguration model)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(toolService);
        ArgumentNullException.ThrowIfNull(model);

        return _hyperlight.HasGuest
            ? CreateHyperlight(chatClient, toolService, model, _hyperlight.GuestPath!)
            : CreateOffline(chatClient, model);
    }

    /// <inheritdoc />
    public string ComputeToolFingerprint(PortfolioToolService toolService) =>
        ToolCatalog.ComputeFingerprint(_catalog.CreateTools(toolService));

    private AgentBundle CreateHyperlight(IChatClient chatClient, PortfolioToolService toolService, ModelConfiguration model, string guestPath)
    {
        var providerOwnedTools = _catalog.CreateTools(toolService).ToList();

        var options = HyperlightCodeActProviderOptions.CreateForWasm(guestPath);
        options.Tools = providerOwnedTools;
        // Security defaults are intentionally left strict: no AllowedDomains (network denied),
        // no FileMounts (no host filesystem), ApprovalMode NeverRequire (unattended benchmark run).

        var provider = new HyperlightCodeActProvider(options);
        var agent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "sterling-vale-codeact",
            ChatOptions = new ChatOptions
            {
                ModelId = model.DeploymentName,
                Instructions = Prompt.CodeAct,
                Temperature = model.Temperature,
                MaxOutputTokens = 16384,
                ResponseFormat = ChatResponseFormat.Json,
            },
            AIContextProviders = [provider],
        });

        return new AgentBundle(agent, provider);
    }

    private static AgentBundle CreateOffline(IChatClient chatClient, ModelConfiguration model)
    {
        var executeCode = FakeExecuteCodeFunction.Create();
        var agent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "sterling-vale-codeact",
            ChatOptions = new ChatOptions
            {
                ModelId = model.DeploymentName,
                Instructions = Prompt.CodeAct,
                Temperature = model.Temperature,
                ResponseFormat = ChatResponseFormat.Json,
                Tools = [executeCode],
            },
        });

        return new AgentBundle(agent);
    }
}
