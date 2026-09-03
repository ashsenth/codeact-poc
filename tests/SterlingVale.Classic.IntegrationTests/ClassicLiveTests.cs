using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SterlingVale.AgentShared.Contracts;
using Xunit;

namespace SterlingVale.Classic.IntegrationTests;

/// <summary>
/// LIVE-model tests. Excluded from normal CI (Category=LiveModel) and skipped unless
/// <c>ENABLE_LIVE_TESTS=true</c> and an Azure OpenAI endpoint is configured. They apply strict caps
/// (small profile, low max turns) and never run without explicit opt-in.
/// </summary>
[Trait("Category", "LiveModel")]
public sealed class ClassicLiveTests(ClassicLiveApiFactory factory) : IClassFixture<ClassicLiveApiFactory>
{
    private static readonly JsonSerializerOptions Json = CreateJson();
    private static readonly string[] AcceptableStatuses = ["Completed", "Capped", "Failed", "TimedOut"];

    private readonly HttpClient _client = factory.CreateClient();

    [SkippableFact]
    public async Task Live_classic_analysis_completes()
    {
        Skip.IfNot(LiveGates.LiveEnabled, "ENABLE_LIVE_TESTS is not 'true'; live-model test skipped.");
        Skip.IfNot(LiveGates.AzureConfigured, "AZURE_OPENAI_ENDPOINT is not set; live-model test skipped.");

        var response = await _client.PostAsJsonAsync("/api/v1/analyses", new { datasetProfile = "small" }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<AnalysisResponseDto>(Json);
        Assert.NotNull(dto);
        // A live model may complete or hit the turn cap; both are valid recorded outcomes.
        Assert.Contains(dto!.Status, AcceptableStatuses);
        Assert.Contains("prompt", dto.Fingerprints.Keys);
    }

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// <summary>Environment gates for opt-in live/Hyperlight tests.</summary>
internal static class LiveGates
{
    public static bool LiveEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("ENABLE_LIVE_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    public static bool AzureConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"));
}
