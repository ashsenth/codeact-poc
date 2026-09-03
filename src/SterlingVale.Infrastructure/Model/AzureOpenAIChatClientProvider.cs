using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.Infrastructure.Configuration;

namespace SterlingVale.Infrastructure.Model;

/// <summary>
/// Live <see cref="IChatClientProvider"/> backed by Azure OpenAI. Prefers
/// <see cref="DefaultAzureCredential"/>; falls back to an API key only when one is configured.
/// Used only when an endpoint is configured — otherwise the app uses the offline fake provider.
/// </summary>
public sealed class AzureOpenAIChatClientProvider : IChatClientProvider
{
    private readonly AzureOpenAIClient _client;
    private readonly string _deployment;

    /// <summary>Creates the provider from Azure OpenAI options.</summary>
    public AzureOpenAIChatClientProvider(AzureOpenAIOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException("Azure OpenAI endpoint is not configured.");
        }

        var endpoint = new Uri(options.Endpoint);
        _client = string.IsNullOrWhiteSpace(options.ApiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new AzureKeyCredential(options.ApiKey));
        _deployment = options.DeploymentName;
    }

    /// <inheritdoc />
    public bool IsLive => true;

    /// <inheritdoc />
    public string ModelId => _deployment;

    /// <inheritdoc />
    public IChatClient CreateChatClient(ChatClientRequest request) =>
        _client.GetChatClient(_deployment).AsIChatClient();
}
