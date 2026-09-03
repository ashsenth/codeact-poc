using SterlingVale.AgentShared.Contracts;
using Xunit;

namespace SterlingVale.AgentShared.Tests;

public sealed class ApiSchemasTests
{
    [Theory]
    [InlineData("{\"datasetProfile\":\"small\"}")]
    [InlineData("{\"scenario\":\"ExposureAndRebalance\",\"datasetProfile\":\"medium\"}")]
    [InlineData("{\"householdIds\":[\"H0001\"],\"datasetProfile\":\"small\"}")]
    [InlineData("{}")]
    public void Valid_requests_pass(string json) => Assert.True(ApiSchemas.IsValidRequest(json));

    [Theory]
    [InlineData("{\"datasetProfile\":\"small\",\"unknown\":1}")]  // additionalProperties: false
    [InlineData("{\"scenario\":\"Nonsense\"}")]                    // enum violation
    [InlineData("{\"householdIds\":\"not-an-array\"}")]            // wrong type
    [InlineData("not json")]
    public void Invalid_requests_fail(string json) => Assert.False(ApiSchemas.IsValidRequest(json));

    [Fact]
    public void Valid_response_passes()
    {
        const string json =
            "{\"runId\":\"r\",\"mode\":\"classic\",\"status\":\"Completed\",\"report\":null," +
            "\"metrics\":{\"durationMs\":1,\"modelRequestCount\":1,\"modelTurnCount\":1,\"toolCallCount\":1," +
            "\"toolCallsByName\":{},\"executeCodeCallCount\":0,\"inputTokens\":null,\"outputTokens\":null," +
            "\"totalTokens\":null,\"estimatedCostUsd\":null,\"retryCount\":0,\"schemaValid\":true}," +
            "\"fingerprints\":{\"prompt\":\"x\"}}";
        Assert.True(ApiSchemas.IsValidResponse(json));
    }

    [Fact]
    public void Response_missing_metrics_fails()
    {
        const string json = "{\"runId\":\"r\",\"mode\":\"classic\",\"status\":\"Completed\",\"fingerprints\":{}}";
        Assert.False(ApiSchemas.IsValidResponse(json));
    }
}
