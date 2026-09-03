using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.Domain.Analysis;

namespace SterlingVale.Api.Hosting;

/// <summary>
/// Maps the identical versioned analysis contract for both APIs. Because both modes call this exact
/// method, the request/response schema, routes, and error contracts are guaranteed identical.
/// </summary>
public static class AnalysisEndpoints
{
    private static readonly JsonSerializerOptions RequestJson = CreateRequestJson();

    /// <summary>Maps health and analysis endpoints onto the application.</summary>
    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));

        app.MapGet("/health/ready", (ISnapshotProvider snapshots) =>
        {
            var readiness = snapshots.Validate();
            return readiness.IsReady
                ? Results.Ok(new { status = "ready", profiles = snapshots.AvailableProfiles })
                : Results.Json(
                    new { status = "not_ready", errors = readiness.Errors },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        app.MapPost("/api/v1/analyses", async (
            JsonElement body,
            ISnapshotProvider snapshots,
            AnalysisService analysisService,
            IRunStore runStore,
            CancellationToken cancellationToken) =>
        {
            // Validate the raw body against the versioned request schema (rejects unknown/invalid fields).
            string rawJson = body.GetRawText();
            if (!ApiSchemas.IsValidRequest(rawJson))
            {
                return Results.Problem(
                    title: "Invalid request",
                    detail: "The request body does not conform to the AnalysisRequest schema.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var request = JsonSerializer.Deserialize<AnalysisRequest>(rawJson, RequestJson) ?? new AnalysisRequest();

            if (!snapshots.AvailableProfiles.Contains(request.DatasetProfile, StringComparer.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    title: "Invalid dataset profile",
                    detail: $"Dataset profile '{request.DatasetProfile}' is not available.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var record = await analysisService.AnalyzeAsync(request, cancellationToken);
            runStore.Save(record);
            return Results.Ok(RunRecordMapper.ToResponseDto(record));
        });

        app.MapGet("/api/v1/analyses/{runId}", (string runId, IRunStore runStore) =>
        {
            var record = runStore.Find(runId);
            return record is null
                ? Results.Problem(title: "Run not found", statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(RunRecordMapper.ToResponseDto(record));
        });

        app.MapGet("/api/v1/analyses/{runId}/metrics", (string runId, IRunStore runStore) =>
        {
            var record = runStore.Find(runId);
            return record is null
                ? Results.Problem(title: "Run not found", statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(RunRecordMapper.ToMetricsDto(record.Response.Metrics));
        });

        app.MapGet("/api/v1/analyses/{runId}/artifacts", (string runId, IRunStore runStore) =>
        {
            var record = runStore.Find(runId);
            return record is null
                ? Results.Problem(title: "Run not found", statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(RunRecordMapper.ToArtifactsDto(record));
        });

        return app;
    }

    private static JsonSerializerOptions CreateRequestJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
