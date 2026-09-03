using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace SterlingVale.AgentShared.Contracts;

/// <summary>
/// Versioned JSON Schemas for the API request and response envelopes. Both APIs serve the identical
/// contract (they share the endpoint mapping), and these schemas make that contract explicit and
/// checkable. The <c>ExposureReport</c> payload has its own schema in
/// <see cref="Prompting.OutputSchema"/>; here the report is treated as an opaque object/null.
/// </summary>
public static class ApiSchemas
{
    /// <summary>Stable identifier for the request schema revision.</summary>
    public const string RequestSchemaId = "analysis-request/v1";

    /// <summary>Stable identifier for the response schema revision.</summary>
    public const string ResponseSchemaId = "analysis-response/v1";

    /// <summary>The canonical AnalysisRequest JSON Schema.</summary>
    public const string RequestSchemaText =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://sterlingvale.example/schemas/analysis-request/v1",
          "title": "AnalysisRequest",
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "scenario": { "type": "string", "enum": ["ExposureAndRebalance"] },
            "householdIds": {
              "type": ["array", "null"],
              "items": { "type": "string", "minLength": 1 }
            },
            "datasetProfile": { "type": "string", "minLength": 1 }
          }
        }
        """;

    /// <summary>The canonical AnalysisResponse JSON Schema.</summary>
    public const string ResponseSchemaText =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://sterlingvale.example/schemas/analysis-response/v1",
          "title": "AnalysisResponse",
          "type": "object",
          "additionalProperties": false,
          "required": ["runId", "mode", "status", "metrics", "fingerprints"],
          "properties": {
            "runId": { "type": "string", "minLength": 1 },
            "mode": { "type": "string", "enum": ["classic", "codeact"] },
            "status": { "type": "string", "enum": ["Completed", "Capped", "TimedOut", "Failed"] },
            "report": { "type": ["object", "null"] },
            "metrics": {
              "type": "object",
              "required": ["durationMs", "modelRequestCount", "modelTurnCount", "toolCallCount", "executeCodeCallCount", "schemaValid"],
              "properties": {
                "durationMs": { "type": "number" },
                "modelRequestCount": { "type": "integer" },
                "modelTurnCount": { "type": "integer" },
                "toolCallCount": { "type": "integer" },
                "toolCallsByName": { "type": "object" },
                "executeCodeCallCount": { "type": "integer" },
                "inputTokens": { "type": ["integer", "null"] },
                "outputTokens": { "type": ["integer", "null"] },
                "totalTokens": { "type": ["integer", "null"] },
                "estimatedCostUsd": { "type": ["number", "null"] },
                "retryCount": { "type": "integer" },
                "schemaValid": { "type": "boolean" }
              }
            },
            "fingerprints": {
              "type": "object",
              "additionalProperties": { "type": "string" }
            }
          }
        }
        """;

    private static readonly JsonSchema CompiledRequest = JsonSchema.FromText(RequestSchemaText);
    private static readonly JsonSchema CompiledResponse = JsonSchema.FromText(ResponseSchemaText);

    /// <summary>Validates a JSON string against the request schema.</summary>
    public static bool IsValidRequest(string json) => IsValid(CompiledRequest, json);

    /// <summary>Validates a JSON string against the response schema.</summary>
    public static bool IsValidResponse(string json) => IsValid(CompiledResponse, json);

    private static bool IsValid(JsonSchema schema, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return false;
        }

        return schema.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.Flag }).IsValid;
    }
}
