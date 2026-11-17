using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.Infrastructure.Configuration;
using SterlingVale.Telemetry;

#pragma warning disable OPENAI001 // The pinned SDK marks the required Responses API experimental.
namespace SterlingVale.Infrastructure.Model;

public sealed class StatelessResponsesChatClient(
    IChatClient inner, AzureOpenAIOptions deployment, Func<CancellationToken, Task> verifyDeployment)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (options?.ConversationId is not null || options?.Tools?.Any(tool => tool is not AIFunction) == true)
            throw new InvalidOperationException("Stateless benchmark Responses requests allow only function tools and no conversation ID.");
        await verifyDeployment(cancellationToken).ConfigureAwait(false);
        var configured = options?.Clone() ?? new ChatOptions();
        configured.RawRepresentationFactory = _ =>
        {
            var raw = new CreateResponseOptions { StoredOutputEnabled = false };
            raw.IncludedProperties.Add("reasoning.encrypted_content");
            return raw;
        };
        var response = await base.GetResponseAsync(messages, configured, cancellationToken).ConfigureAwait(false);
        VerifyModel(response.ModelId, deployment.ModelName!, deployment.ModelVersion!);
        if (response.ConversationId is not null)
            throw new InvalidOperationException("The Responses adapter returned server-side conversation state despite store=false.");
        if (response.RawRepresentation is ResponseResult rawResponse)
        {
            using var document = JsonDocument.Parse(System.ClientModel.Primitives.ModelReaderWriter.Write(rawResponse));
            var usage = document.RootElement.GetProperty("usage");
            if (usage.ValueKind == JsonValueKind.Object)
            {
                ExecutionSession.Current?.Emit("RESPONSES_USAGE_RECEIVED", data: usage.Clone());
                if (usage.TryGetProperty("input_tokens_details", out var details) &&
                    details.TryGetProperty("cache_write_tokens", out var writes))
                {
                    if (response.Usage is null)
                        throw new InvalidDataException("Raw Responses usage was not mapped by the SDK.");
                    response.Usage.AdditionalCounts ??= new();
                    response.Usage.AdditionalCounts["CacheWriteTokenCount"] = writes.GetInt64();
                }
            }
        }
        await verifyDeployment(cancellationToken).ConfigureAwait(false);
        return response;
    }

    public static void VerifyModel(string? actual, string modelName, string modelVersion)
    {
        if (actual != modelName && actual != $"{modelName}-{modelVersion}")
            throw new ModelRequestException(new("DeploymentVersionMismatch",
                "The response model differs from the cohort's pinned deployment model/version."), new InvalidDataException());
    }
}
