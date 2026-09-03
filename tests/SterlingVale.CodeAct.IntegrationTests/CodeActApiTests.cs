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
    public async Task Generated_code_artifact_captures_execute_code_program()
    {
        var created = await PostAnalysisAsync("small");
        string codeFile = Path.Combine(factory.ArtifactsRoot, "runs", created.RunId, "generated-code.txt");
        Assert.True(File.Exists(codeFile));
        // Offline CodeAct captures the execute_code 'code' argument, not the unavailable marker.
        Assert.DoesNotContain("UNAVAILABLE", await File.ReadAllTextAsync(codeFile), StringComparison.Ordinal);
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
