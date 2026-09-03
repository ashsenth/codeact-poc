using System.Text.Json;
using System.Text.Json.Serialization;

namespace SterlingVale.DataGenerator;

/// <summary>Canonical, stable JSON options used for all generated data so hashes are reproducible.</summary>
public static class CanonicalJson
{
    /// <summary>
    /// Shared options: indented for readability, camelCase, no non-deterministic behavior. Property
    /// order is fixed by declaration order in the DTOs, which yields byte-stable output.
    /// </summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            NewLine = "\n",
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
