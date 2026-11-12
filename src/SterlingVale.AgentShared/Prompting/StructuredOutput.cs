using System.Text.Json;
using System.Text.Json.Nodes;

namespace SterlingVale.AgentShared.Prompting;

public static class StructuredOutput
{
    public const string SchemaName = "exposure_report_v2";
    public static JsonElement Schema { get; } = BuildSchema();
    public static string SchemaHash { get; } = Hashing.Sha256Hex(Schema.GetRawText());

    private static JsonElement BuildSchema()
    {
        var schema = JsonNode.Parse(OutputSchema.SchemaText)!.AsObject();
        schema.Remove("$schema");
        schema.Remove("$id");
        // Azure cannot constrain cross-field conditionals or string lengths. Full v2 validation remains local.
        schema.Remove("allOf");
        RemoveLengthConstraints(schema);
        return JsonSerializer.SerializeToElement(schema);
    }

    private static void RemoveLengthConstraints(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            obj.Remove("minLength");
            foreach (var property in obj)
                RemoveLengthConstraints(property.Value);
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
                RemoveLengthConstraints(item);
        }
    }
}
