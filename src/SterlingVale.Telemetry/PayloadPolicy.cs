using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SterlingVale.Telemetry;

public enum PayloadCaptureMode { None, Metadata, Full }

public sealed record CapturedPayload(string? Content, int OriginalLength, bool Truncated, bool Redacted, string CaptureMode);

public sealed partial class PayloadPolicy(PayloadCaptureMode mode = PayloadCaptureMode.Metadata, int maximumCharacters = 65536)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public PayloadCaptureMode Mode { get; } = mode;
    public int MaximumCharacters { get; } = maximumCharacters > 0 ? maximumCharacters : throw new ArgumentOutOfRangeException(nameof(maximumCharacters));

    public CapturedPayload Capture(object? value)
    {
        string text = value is string literal ? literal : JsonSerializer.Serialize(value, Json);
        if (Mode != PayloadCaptureMode.Full)
        {
            return new CapturedPayload(null, Mode == PayloadCaptureMode.None ? 0 : text.Length, false, false, Mode.ToString().ToLowerInvariant());
        }

        string safe = Redact(text);
        return new CapturedPayload(safe.Length > MaximumCharacters ? safe[..MaximumCharacters] : safe,
            text.Length, safe.Length > MaximumCharacters, safe != text, "full");
    }

    public static string Redact(string text)
    {
        string safe = text;
        try
        {
            var node = JsonNode.Parse(text);
            if (RedactNode(node))
            {
                safe = node?.ToJsonString() ?? text;
            }
        }
        catch (JsonException)
        {
        }

        return CredentialPattern().Replace(safe, "$1[REDACTED]");
    }

    private static bool RedactNode(JsonNode? node)
    {
        bool changed = false;
        if (node is JsonObject properties)
        {
            foreach (var key in properties.Select(property => property.Key).ToArray())
            {
                if (SecretKeyPattern().IsMatch(key))
                {
                    properties[key] = "[REDACTED]";
                    changed = true;
                }
                else
                {
                    changed |= RedactNode(properties[key]);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                changed |= RedactNode(child);
            }
        }

        return changed;
    }

    [GeneratedRegex("api[-_]?key|authorization|password|passwd|secret|token|connectionstring|credential", RegexOptions.IgnoreCase)]
    private static partial Regex SecretKeyPattern();

    [GeneratedRegex("((?:Bearer\\s+)|(?:(?:api[-_]?key|password|secret|access_token)\\s*[=:]\\s*[\"']?))[^\\s\"',;}]+", RegexOptions.IgnoreCase)]
    private static partial Regex CredentialPattern();
}