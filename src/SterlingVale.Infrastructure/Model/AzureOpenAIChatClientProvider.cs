using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.Infrastructure.Configuration;
using SterlingVale.Telemetry;

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
        var clientOptions = new AzureOpenAIClientOptions();
        clientOptions.AddPolicy(new ExecutionHeaderPolicy(), PipelinePosition.PerCall);
        _client = string.IsNullOrWhiteSpace(options.ApiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential(), clientOptions)
            : new AzureOpenAIClient(endpoint, new AzureKeyCredential(options.ApiKey), clientOptions);
        _deployment = options.DeploymentName;
    }

    /// <inheritdoc />
    public bool IsLive => true;

    /// <inheritdoc />
    public string ModelId => _deployment;

    /// <inheritdoc />
    public IChatClient CreateChatClient(ChatClientRequest request) =>
        _client.GetChatClient(_deployment).AsIChatClient();

    private sealed class ExecutionHeaderPolicy : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            AddHeaders(message);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            AddHeaders(message);
            return ProcessNextAsync(message, pipeline, currentIndex);
        }

        private static void AddHeaders(PipelineMessage message)
        {
            if (ExecutionSession.Current is { } session)
            {
                message.Request.Headers.Set("X-Correlation-ID", session.Context.CorrelationId);
                message.Request.Headers.Set("X-Run-ID", session.Context.RunId);
                message.Request.Headers.Set("X-Comparison-ID", session.Context.ComparisonId);
                message.Request.Headers.Set("X-Trial-ID", session.Context.TrialId);
                if (System.Diagnostics.Activity.Current is { } activity)
                {
                    message.Request.Headers.Set("traceparent", activity.Id!);
                }
            }
        }
    }
}
