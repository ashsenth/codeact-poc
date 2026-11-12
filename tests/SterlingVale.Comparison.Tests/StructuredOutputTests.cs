using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using OpenAI;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.AgentShared.Prompting;
using Xunit;

namespace SterlingVale.Comparison.Tests;

public sealed class StructuredOutputTests
{
    [Fact]
    public async Task OpenAI_adapter_sends_non_strict_v2_schema_with_parallel_tools_and_keeps_validation_local()
    {
        using var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var sdk = new OpenAIClient(new ApiKeyCredential("test-key"), new OpenAIClientOptions
        {
            Endpoint = new Uri("https://structured.invalid/"),
            Transport = new HttpClientPipelineTransport(http),
        });
        using var client = sdk.GetChatClient("test-model").AsIChatClient();
        var model = new ModelConfiguration { DeploymentName = "test-model" };
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Return JSON")],
            model.CreateChatOptions("Return the exposure report as JSON.",
                [AIFunctionFactory.Create(() => "[]", "list_households")]));

        using var request = JsonDocument.Parse(Assert.IsType<string>(handler.Body));
        var body = request.RootElement;
        var format = body.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        var jsonSchema = format.GetProperty("json_schema");
        Assert.False(jsonSchema.GetProperty("strict").GetBoolean());
        Assert.Equal(StructuredOutput.SchemaName, jsonSchema.GetProperty("name").GetString());
        Assert.True(body.GetProperty("parallel_tool_calls").GetBoolean());
        Assert.False(body.GetProperty("tools")[0].GetProperty("function").GetProperty("strict").GetBoolean());
        var schema = jsonSchema.GetProperty("schema");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(StructuredOutput.Schema.GetRawText()), JsonNode.Parse(schema.GetRawText())));
        Assert.False(schema.TryGetProperty("allOf", out _));
        Assert.DoesNotContain("\"minLength\"", schema.GetRawText());

        var validator = Json.Schema.JsonSchema.FromText(schema.GetRawText());
        const string valid = """
            {"runId":"r","generatedAt":"2026-01-01","status":"incomplete","missingItems":["price for X"],"households":[]}
            """;
        Assert.True(validator.Evaluate(JsonNode.Parse(valid)).IsValid);
        Assert.True(OutputSchema.IsValid(valid));
        Assert.False(validator.Evaluate(JsonNode.Parse(valid.Replace("\"incomplete\"", "\"unknown\"", StringComparison.Ordinal))).IsValid);
        string missingEvidence = valid.Replace("[\"price for X\"]", "[]", StringComparison.Ordinal);
        Assert.True(validator.Evaluate(JsonNode.Parse(missingEvidence)).IsValid);
        Assert.False(OutputSchema.IsValid(missingEvidence));
        const string tradeReport = """
            {"runId":"r","generatedAt":"2026-01-01","status":"complete","missingItems":[],"households":[
              {"householdId":"H1","baseCurrency":"USD","totalValue":1,"flagged":false,
               "drifts":[],"breaches":[],"proposedTrades":[
                 {"assetClass":"Crypto","direction":"Buy","notional":0,"currency":"USD"}]}]}
            """;
        Assert.True(validator.Evaluate(JsonNode.Parse(tradeReport)).IsValid);
        Assert.False(validator.Evaluate(JsonNode.Parse(tradeReport.Replace("\"Buy\"", "\"Hold\"", StringComparison.Ordinal))).IsValid);
        Assert.False(OutputSchema.IsValid(tradeReport.Replace("\"Buy\"", "\"Hold\"", StringComparison.Ordinal)));
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"id":"test","object":"chat.completion","created":0,"model":"test-model",
                     "choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"{}"}}],
                     "usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}
                    """, Encoding.UTF8, "application/json"),
            };
        }
    }
}
