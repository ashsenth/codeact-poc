using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SterlingVale.AgentShared.Contracts;
using Xunit;

namespace SterlingVale.CodeAct.IntegrationTests;

public sealed class CodeActApiTests(CodeActApiFactory factory) : IClassFixture<CodeActApiFactory>
{
    private static readonly JsonSerializerOptions Json = CreateJson();

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_ready_reports_small_profile()
    {
        var response = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("small", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_analysis_completes_via_execute_code_surface()
    {
        var result = await PostAnalysisAsync("small");
        Assert.Equal("Completed", result.Status);
        Assert.NotNull(result.Report);
        Assert.True(result.Metrics.SchemaValid);
        // CodeAct exercises the execute_code surface exactly once in the offline path.
        Assert.Equal(1, result.Metrics.ExecuteCodeCallCount);
    }

    [Fact]
    public async Task Response_reports_codeact_mode_and_fingerprints()
    {
        var result = await PostAnalysisAsync("small");
        Assert.Equal("codeact", result.Mode);
        Assert.Contains("tools", result.Fingerprints.Keys);
        Assert.Contains("prompt", result.Fingerprints.Keys);
    }

    [Fact]
    public async Task Run_is_retrievable_with_artifacts()
    {
        var created = await PostAnalysisAsync("small");
        var artifacts = await _client.GetFromJsonAsync<ArtifactsDto>($"/api/v1/analyses/{created.RunId}/artifacts", Json);
        Assert.NotNull(artifacts);
        Assert.False(string.IsNullOrWhiteSpace(artifacts!.RawModelOutput));
    }

    [Fact]
    public async Task Metadata_mode_records_code_execution_without_persisting_program_payload()
    {
        var created = await PostAnalysisAsync("small");
        string directory = Path.Combine(factory.ArtifactsRoot, "runs", created.RunId);
        Assert.False(File.Exists(Path.Combine(directory, "codeact/generated-code-001.py")));
        Assert.True(File.Exists(Path.Combine(directory, "codeact/execution-001.json")));
        Assert.True(File.Exists(Path.Combine(directory, "timeline.json")));
        string events = await File.ReadAllTextAsync(Path.Combine(directory, "events.jsonl"));
        Assert.Contains("CODE_GENERATED", events, StringComparison.Ordinal);
        Assert.Contains("OFFLINE_CODE_TOOL_STARTED", events, StringComparison.Ordinal);
        Assert.Contains("OFFLINE_CODE_TOOL_COMPLETED", events, StringComparison.Ordinal);
        Assert.DoesNotContain("CODE_EXECUTION_STARTED", events, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_profile_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/analyses", new { datasetProfile = "gigantic" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Runs_are_deterministic()
    {
        var first = await PostAnalysisAsync("small");
        var second = await PostAnalysisAsync("small");
        Assert.Equal(
            JsonSerializer.Serialize(first.Report, Json),
            JsonSerializer.Serialize(second.Report! with { RunId = first.Report!.RunId }, Json));
    }

    private async Task<AnalysisResponseDto> PostAnalysisAsync(string profile)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/analyses", new { datasetProfile = profile }, Json);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AnalysisResponseDto>(Json);
        Assert.NotNull(dto);
        return dto!;
    }

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
