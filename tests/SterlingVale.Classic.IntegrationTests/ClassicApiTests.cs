using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SterlingVale.AgentShared.Contracts;
using Xunit;

namespace SterlingVale.Classic.IntegrationTests;

public sealed class ClassicApiTests(ClassicApiFactory factory) : IClassFixture<ClassicApiFactory>
{
    private static readonly JsonSerializerOptions Json = CreateJson();

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_live_returns_ok()
    {
        var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_ready_reports_small_profile()
    {
        var response = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("small", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_analysis_returns_completed_schema_valid_report()
    {
        var result = await PostAnalysisAsync("small");
        Assert.Equal("Completed", result.Status);
        Assert.NotNull(result.Report);
        Assert.True(result.Metrics.ToolCallCount >= 1);
        Assert.True(result.Metrics.SchemaValid);
        Assert.Contains("prompt", result.Fingerprints.Keys);
        Assert.Contains("tools", result.Fingerprints.Keys);
        Assert.Contains("dataset", result.Fingerprints.Keys);
    }

    [Fact]
    public async Task Run_can_be_retrieved_with_metrics_and_artifacts()
    {
        var created = await PostAnalysisAsync("small");

        var fetched = await _client.GetFromJsonAsync<AnalysisResponseDto>($"/api/v1/analyses/{created.RunId}", Json);
        Assert.NotNull(fetched);
        Assert.Equal(created.RunId, fetched!.RunId);

        var metrics = await _client.GetFromJsonAsync<RunMetricsDto>($"/api/v1/analyses/{created.RunId}/metrics", Json);
        Assert.NotNull(metrics);

        var artifacts = await _client.GetFromJsonAsync<ArtifactsDto>($"/api/v1/analyses/{created.RunId}/artifacts", Json);
        Assert.NotNull(artifacts);
        Assert.False(string.IsNullOrWhiteSpace(artifacts!.RawModelOutput));
    }

    [Fact]
    public async Task Unknown_run_returns_404()
    {
        var response = await _client.GetAsync("/api/v1/analyses/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Artifacts_are_persisted_to_disk()
    {
        var created = await PostAnalysisAsync("small");
        string runDir = Path.Combine(factory.ArtifactsRoot, "runs", created.RunId);
        Assert.True(File.Exists(Path.Combine(runDir, "result.json")));
        Assert.True(File.Exists(Path.Combine(runDir, "metrics.json")));
        Assert.True(File.Exists(Path.Combine(runDir, "events.jsonl")));
        Assert.True(File.Exists(Path.Combine(runDir, "metadata.json")));
        Assert.True(File.Exists(Path.Combine(runDir, "generated-code.txt")));
    }

    [Fact]
    public async Task Invalid_profile_returns_400_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/analyses", new { datasetProfile = "gigantic" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_request_field_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/analyses", new { datasetProfile = "small", bogus = 1 }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Runs_are_deterministic()
    {
        var first = await PostAnalysisAsync("small");
        var second = await PostAnalysisAsync("small");
        // Same oracle-backed fake output; reports must be identical bar the run id.
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
