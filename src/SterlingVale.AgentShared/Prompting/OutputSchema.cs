using System.Text.Json.Nodes;
using Json.Schema;

namespace SterlingVale.AgentShared.Prompting;

/// <summary>
/// The versioned JSON Schema (2020-12) for the ExposureReport wire contract, plus a validator.
/// This single schema is shared by both modes; its hash is a fairness fingerprint.
/// </summary>
public static class OutputSchema
{
    /// <summary>Stable identifier for this schema revision.</summary>
    public const string SchemaId = "exposure-report/v2";

    private static readonly JsonSchema Compiled = JsonSchema.FromText(SchemaText);

    /// <summary>The canonical JSON Schema text.</summary>
    public const string SchemaText =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://sterlingvale.example/schemas/exposure-report/v2",
          "title": "ExposureReport",
          "type": "object",
          "additionalProperties": false,
          "required": ["runId", "generatedAt", "status", "missingItems", "households"],
          "allOf": [
            {
              "if": { "properties": { "status": { "const": "incomplete" } } },
              "then": { "properties": { "missingItems": { "minItems": 1 } } },
              "else": { "properties": { "missingItems": { "maxItems": 0 } } }
            }
          ],
          "properties": {
            "runId": { "type": "string", "minLength": 1 },
            "generatedAt": { "type": "string", "minLength": 1 },
            "status": { "type": "string", "enum": ["complete", "incomplete"] },
            "missingItems": {
              "type": "array",
              "items": { "type": "string", "minLength": 1 },
              "description": "Specific missing evidence, for example symbols without prices. Non-empty when incomplete; empty when complete."
            },
            "households": {
              "type": "array",
              "description": "Only household reports supported by available evidence. May be empty when incomplete; do not invent numeric placeholders.",
              "items": { "$ref": "#/$defs/household" }
            }
          },
          "$defs": {
            "assetClass": { "type": "string", "enum": ["Equity", "FixedIncome", "Cash", "Alternative", "Crypto"] },
            "currency": { "type": "string", "enum": ["USD", "EUR", "GBP", "JPY"] },
            "household": {
              "type": "object",
              "additionalProperties": false,
              "required": ["householdId", "baseCurrency", "totalValue", "flagged", "drifts", "breaches", "proposedTrades"],
              "properties": {
                "householdId": { "type": "string", "minLength": 1 },
                "baseCurrency": { "$ref": "#/$defs/currency" },
                "totalValue": { "type": "number" },
                "flagged": { "type": "boolean" },
                "drifts": { "type": "array", "items": { "$ref": "#/$defs/drift" } },
                "breaches": { "type": "array", "items": { "$ref": "#/$defs/breach" } },
                "proposedTrades": { "type": "array", "items": { "$ref": "#/$defs/trade" } }
              }
            },
            "drift": {
              "type": "object",
              "additionalProperties": false,
              "required": ["assetClass", "actualFraction", "targetFraction", "driftFraction", "breached"],
              "properties": {
                "assetClass": { "$ref": "#/$defs/assetClass" },
                "actualFraction": { "type": "number" },
                "targetFraction": { "type": "number" },
                "driftFraction": { "type": "number" },
                "breached": { "type": "boolean" }
              }
            },
            "breach": {
              "type": "object",
              "additionalProperties": false,
              "required": ["kind", "detail", "observed", "limit"],
              "properties": {
                "kind": { "type": "string", "enum": ["Concentration", "FxExposure", "CryptoExposure", "AllocationDrift"] },
                "detail": { "type": "string" },
                "observed": { "type": "number" },
                "limit": { "type": "number" }
              }
            },
            "trade": {
              "type": "object",
              "additionalProperties": false,
              "required": ["assetClass", "direction", "notional", "currency"],
              "properties": {
                "assetClass": { "$ref": "#/$defs/assetClass" },
                "direction": { "type": "string", "enum": ["Buy", "Sell"] },
                "notional": { "type": "number" },
                "currency": { "$ref": "#/$defs/currency" }
              }
            }
          }
        }
        """;

    /// <summary>Validates a JSON string against the schema. Returns true when valid.</summary>
    public static bool IsValid(string json)
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
        catch (System.Text.Json.JsonException)
        {
            return false;
        }

        var results = Compiled.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.Flag });
        return results.IsValid;
    }
}
