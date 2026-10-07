using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using SterlingVale.Tools.Observability;

namespace SterlingVale.Tools;

/// <summary>
/// The single canonical source of tool registrations. Both the Classic agent (direct tools) and the
/// CodeAct agent (provider-owned tools reachable via <c>call_tool</c>) build their tool lists from
/// this catalog, guaranteeing identical names, descriptions, and schemas. Divergence would confound
/// the benchmark, so a fingerprint is provided for fairness assertions.
/// </summary>
public sealed class ToolCatalog
{
    /// <summary>Creates the seven canonical <see cref="AIFunction"/> tools over the given service.</summary>
    /// <remarks>
    /// Each tool is wrapped in a <see cref="RecordingAIFunction"/> so every invocation (including
    /// CodeAct's in-sandbox <c>call_tool</c>, which never surfaces in the model messages) is captured
    /// by the ambient <see cref="ToolCallRecorder"/>. The wrapper delegates name/description/schema, so
    /// the fairness fingerprint is unaffected.
    /// </remarks>
    public IReadOnlyList<AIFunction> CreateTools(PortfolioToolService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        return
        [
            new RecordingAIFunction(AIFunctionFactory.Create(service.ListHouseholds, ToolNames.ListHouseholds)),
            new RecordingAIFunction(AIFunctionFactory.Create(service.GetPolicy, ToolNames.GetPolicy)),
            new RecordingAIFunction(AIFunctionFactory.Create(service.ListAccounts, ToolNames.ListAccounts)),
            new RecordingAIFunction(AIFunctionFactory.Create(service.ListPositions, ToolNames.ListPositions)),
            new RecordingAIFunction(AIFunctionFactory.Create(service.GetPrice, ToolNames.GetPrice)),
            new RecordingAIFunction(AIFunctionFactory.Create(service.GetFx, ToolNames.GetFx)),
            new RecordingAIFunction(AIFunctionFactory.Create(service.AssetClass, ToolNames.AssetClass)),
        ];
    }

    /// <summary>
    /// Computes a deterministic SHA-256 fingerprint over the tools' names, descriptions, and JSON
    /// schemas (sorted by name). Both modes must produce the same value, or a comparison fails.
    /// </summary>
    public static string ComputeFingerprint(IEnumerable<AIFunction> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        var builder = new StringBuilder();
        foreach (var tool in tools.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            builder.Append(tool.Name).Append('\n');
            builder.Append(tool.Description).Append('\n');
            builder.Append(SerializeSchema(tool.JsonSchema)).Append("\n---\n");
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash);
    }

    private static string SerializeSchema(JsonElement schema) =>
        JsonSerializer.Serialize(schema);
}
