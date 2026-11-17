using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using System.ClientModel.Primitives;
using System.Net.Http.Headers;
using System.Text.Json;
using OpenAI;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.Infrastructure.Configuration;
using SterlingVale.Telemetry;

#pragma warning disable OPENAI001 // The pinned SDK marks the required Responses API experimental.
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
    private readonly AzureOpenAIOptions _settings;
    private readonly OpenAI.Responses.ResponsesClient? _responses;
    private readonly DefaultAzureCredential _credential = new();
    private static readonly HttpClient Management = new();

    /// <summary>Creates the provider from Azure OpenAI options.</summary>
    public AzureOpenAIChatClientProvider(AzureOpenAIOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _settings = options;
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException("Azure OpenAI endpoint is not configured.");
        }

        var endpoint = NormalizeEndpoint(options.Endpoint, out bool normalized);
        if (normalized)
            Console.WriteLine("Azure OpenAI endpoint normalized to the base URL (path/query/fragment removed).");
        var clientOptions = new AzureOpenAIClientOptions();
        clientOptions.RetryPolicy = new ObservedRetryPolicy();
        clientOptions.AddPolicy(new ExecutionHeaderPolicy(), PipelinePosition.PerCall);
        _client = string.IsNullOrWhiteSpace(options.ApiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential(), clientOptions)
            : new AzureOpenAIClient(endpoint, new AzureKeyCredential(options.ApiKey), clientOptions);
        _deployment = options.DeploymentName;
        if (options.ApiMode == "responses")
        {
            if (string.IsNullOrWhiteSpace(options.ModelName) || string.IsNullOrWhiteSpace(options.ModelVersion) ||
                string.IsNullOrWhiteSpace(options.ResourceId) || !options.ResourceId.StartsWith("/subscriptions/", StringComparison.Ordinal))
                throw new InvalidOperationException("Responses benchmarks require pinned deployment model, version and Azure resource ID.");
            var responsesOptions = new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint, "openai/v1/"),
                RetryPolicy = new ObservedRetryPolicy(),
            };
            responsesOptions.AddPolicy(new ExecutionHeaderPolicy(), PipelinePosition.PerCall);
            responsesOptions.AddPolicy(new ResponsesContractPolicy(), PipelinePosition.PerCall);
            var sdk = string.IsNullOrWhiteSpace(options.ApiKey)
                ? new OpenAIClient(new EntraAuthenticationPolicy(_credential), responsesOptions)
                : new OpenAIClient(new System.ClientModel.ApiKeyCredential(options.ApiKey), responsesOptions);
            _responses = sdk.GetResponsesClient();
        }
        else if (options.ApiMode != "chat-completions")
            throw new InvalidOperationException("MODEL_API_MODE must be chat-completions or responses.");
    }

    /// <inheritdoc />
    public bool IsLive => true;

    /// <inheritdoc />
    public string ModelId => _deployment;

    public static Uri NormalizeEndpoint(string value, out bool normalized)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(endpoint.UserInfo))
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT must be an absolute HTTPS resource URL without embedded credentials.");
        var result = new Uri(endpoint.GetLeftPart(UriPartial.Authority) + "/");
        normalized = endpoint.AbsolutePath != "/" || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0;
        return result;
    }

    /// <inheritdoc />
    public IChatClient CreateChatClient(ChatClientRequest request) =>
        new DiagnosticChatClient(_responses is null ? _client.GetChatClient(_deployment).AsIChatClient() :
            new StatelessResponsesChatClient(_responses.AsIChatClient(_deployment), _settings, VerifyDeploymentAsync));

    private async Task VerifyDeploymentAsync(CancellationToken cancellationToken)
    {
        var token = await _credential.GetTokenAsync(new Azure.Core.TokenRequestContext(["https://management.azure.com/.default"]),
            cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://management.azure.com{_settings.ResourceId}/deployments/{Uri.EscapeDataString(_deployment)}?api-version=2024-10-01");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        using var response = await Management.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var properties = document.RootElement.GetProperty("properties");
        var model = properties.GetProperty("model");
        if (model.GetProperty("name").GetString() != _settings.ModelName ||
            model.GetProperty("version").GetString() != _settings.ModelVersion ||
            document.RootElement.GetProperty("sku").GetProperty("name").GetString() != "GlobalStandard")
            throw new ModelRequestException(new("DeploymentVersionMismatch",
                "Deployment metadata changed from the cohort's pinned model/version/GlobalStandard SKU."), new InvalidDataException());
        ExecutionSession.Current?.Emit("DEPLOYMENT_VERIFIED", data: new
        {
            deployment = _deployment, modelName = _settings.ModelName, modelVersion = _settings.ModelVersion,
            sku = "GlobalStandard", versionUpgradeOption = properties.GetProperty("versionUpgradeOption").GetString(),
        });
    }

    public sealed class ResponsesContractPolicy : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            Verify(message);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            Verify(message);
            return ProcessNextAsync(message, pipeline, currentIndex);
        }

        private static void Verify(PipelineMessage message)
        {
            using var stream = new MemoryStream();
            message.Request.Content!.WriteTo(stream, message.CancellationToken);
            using var document = JsonDocument.Parse(stream.ToArray());
            var root = document.RootElement;
            if (!root.TryGetProperty("store", out var store) || store.ValueKind != JsonValueKind.False ||
                root.TryGetProperty("previous_response_id", out var previous) && previous.ValueKind != JsonValueKind.Null ||
                root.TryGetProperty("conversation", out var conversation) && conversation.ValueKind != JsonValueKind.Null ||
                !root.GetProperty("parallel_tool_calls").GetBoolean() ||
                root.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean() ||
                root.GetProperty("tools").EnumerateArray().Any(tool => tool.GetProperty("type").GetString() != "function"))
                throw new InvalidOperationException("Responses wire contract violated: stateless, full-context, non-strict JSON schema, parallel function tools required.");
            ExecutionSession.Current?.Emit("RESPONSES_REQUEST_CONFIGURATION", data: new
            {
                api = "responses", store = false, previousResponseId = (string?)null,
                inputItems = root.GetProperty("input").GetArrayLength(),
                toolType = "function", parallelToolCalls = true, strict = false,
                maxOutputTokens = root.GetProperty("max_output_tokens").GetInt32(),
            });
        }
    }

    private sealed class EntraAuthenticationPolicy(Azure.Core.TokenCredential credential) : AuthenticationPolicy
    {
        private static readonly Azure.Core.TokenRequestContext TokenContext = new(["https://cognitiveservices.azure.com/.default"]);

        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            var token = credential.GetToken(TokenContext, message.CancellationToken);
            message.Request.Headers.Set("Authorization", "Bearer " + token.Token);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            var token = await credential.GetTokenAsync(TokenContext, message.CancellationToken).ConfigureAwait(false);
            message.Request.Headers.Set("Authorization", "Bearer " + token.Token);
            await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
        }
    }

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
