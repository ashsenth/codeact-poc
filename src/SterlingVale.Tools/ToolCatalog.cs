using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

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
    public IReadOnlyList<AIFunction> CreateTools(PortfolioToolService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        return
        [
            AIFunctionFactory.Create(service.ListHouseholds, ToolNames.ListHouseholds),
            AIFunctionFactory.Create(service.GetPolicy, ToolNames.GetPolicy),
            AIFunctionFactory.Create(service.ListAccounts, ToolNames.ListAccounts),
            AIFunctionFactory.Create(service.ListPositions, ToolNames.ListPositions),
            AIFunctionFactory.Create(service.GetPrice, ToolNames.GetPrice),
            AIFunctionFactory.Create(service.GetFx, ToolNames.GetFx),
            AIFunctionFactory.Create(service.AssetClass, ToolNames.AssetClass),
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
