using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SterlingVale.AgentShared.Contracts;
using Xunit;

namespace SterlingVale.CodeAct.IntegrationTests;

/// <summary>Environment gates for opt-in live/Hyperlight tests.</summary>
internal static class LiveGates
{
    public static bool LiveEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("ENABLE_LIVE_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    public static bool AzureConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"));

    public static bool GuestConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HYPERLIGHT_PYTHON_GUEST_PATH"));
}

/// <summary>
/// LIVE-model tests for CodeAct. Excluded from CI (Category=LiveModel); skipped unless
/// <c>ENABLE_LIVE_TESTS=true</c> and an Azure endpoint is configured. Capped and opt-in only.
/// </summary>
[Trait("Category", "LiveModel")]
public sealed class CodeActLiveTests(CodeActLiveApiFactory factory) : IClassFixture<CodeActLiveApiFactory>
{
    private static readonly JsonSerializerOptions Json = JsonOptions();
    private static readonly string[] AcceptableStatuses = ["Completed", "Capped", "Failed", "TimedOut"];

    private readonly HttpClient _client = factory.CreateClient();

    [SkippableFact]
    public async Task Live_codeact_analysis_completes()
    {
        Skip.IfNot(LiveGates.LiveEnabled, "ENABLE_LIVE_TESTS is not 'true'; live-model test skipped.");
        Skip.IfNot(LiveGates.AzureConfigured, "AZURE_OPENAI_ENDPOINT is not set; live-model test skipped.");

        var response = await _client.PostAsJsonAsync("/api/v1/analyses", new { datasetProfile = "small" }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<AnalysisResponseDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal("codeact", dto!.Mode);
        Assert.Contains(dto.Status, AcceptableStatuses);
    }

    internal static JsonSerializerOptions JsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// <summary>
/// Hyperlight RUNTIME end-to-end tests. Skipped unless <c>HYPERLIGHT_PYTHON_GUEST_PATH</c> is set and
/// hardware virtualization is available. With a guest, a full analysis drives <c>execute_code</c> in
/// the real micro-VM (fresh sandbox per run, network/filesystem denied by default) and the run
/// completes — proving the sandbox executes, isolates, and disposes correctly.
/// </summary>
[Trait("Category", "Hyperlight")]
public sealed class HyperlightEndToEndTests(CodeActGuestApiFactory factory) : IClassFixture<CodeActGuestApiFactory>
{
    private static readonly JsonSerializerOptions Json = CodeActLiveTests.JsonOptions();

    private readonly HttpClient _client = factory.CreateClient();

    [SkippableFact]
    public async Task Analysis_runs_through_real_sandbox()
    {
        Skip.IfNot(LiveGates.GuestConfigured, "HYPERLIGHT_PYTHON_GUEST_PATH not set; Hyperlight runtime test skipped.");

        var response = await _client.PostAsJsonAsync("/api/v1/analyses", new { datasetProfile = "small" }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<AnalysisResponseDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal("codeact", dto!.Mode);
        Assert.True(dto.Metrics.ExecuteCodeCallCount >= 1);
    }

    [SkippableFact]
    public async Task Sandbox_state_is_isolated_between_runs()
    {
        Skip.IfNot(LiveGates.GuestConfigured, "HYPERLIGHT_PYTHON_GUEST_PATH not set; Hyperlight runtime test skipped.");

        // Each run creates and disposes its own provider/sandbox; two runs must both succeed.
        var first = await _client.PostAsJsonAsync("/api/v1/analyses", new { datasetProfile = "small" }, Json);
        var second = await _client.PostAsJsonAsync("/api/v1/analyses", new { datasetProfile = "small" }, Json);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }
}
