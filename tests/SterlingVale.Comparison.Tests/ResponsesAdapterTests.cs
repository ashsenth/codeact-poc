using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Configuration;
using SterlingVale.Infrastructure.Configuration;
using SterlingVale.Infrastructure.Model;
using SterlingVale.Telemetry;
using Xunit;

#pragma warning disable OPENAI001 // Exercise the same pinned Responses API as production.
namespace SterlingVale.Comparison.Tests;

public sealed class ResponsesAdapterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sv-responses-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("gpt-4o", "2024-11-20")]
    [InlineData("gpt-6.1-sol", "2026-09-29")]
    public async Task Stateless_adapter_resends_full_function_history_and_preserves_usage(string modelName, string version)
    {
        using var session = new ExecutionSession(_root, "classic", "small");
        using var handler = new ResponsesHandler(modelName, version, callFunction: true);
        using var http = new HttpClient(handler);
        using var client = CreateClient(http, modelName, version, invokeFunctions: true);
        var model = new ModelConfiguration
        {
            DeploymentName = modelName, ModelName = modelName, ModelVersion = version, ApiMode = "responses",
            Temperature = modelName == "gpt-6.1-sol" ? null : 0f,
            DefaultReasoningEffort = modelName == "gpt-6.1-sol" ? "medium" : "not-supported",
        };
        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Return JSON")],
            model.CreateChatOptions("Analyze using the available tools.",
                [AIFunctionFactory.Create(() => "[]", "list_households")]));
        Assert.Equal("{}", response.Text);
        Assert.Equal(2, handler.Requests.Count);
        var first = handler.Requests[0];
        var second = handler.Requests[1];
        Assert.True(second.GetProperty("input").GetArrayLength() > first.GetProperty("input").GetArrayLength());
        Assert.Contains(second.GetProperty("input").EnumerateArray(), item => item.GetProperty("type").GetString() == "function_call_output");
        if (modelName == "gpt-6.1-sol")
            Assert.Contains(second.GetProperty("input").EnumerateArray(), item =>
                item.GetProperty("type").GetString() == "reasoning" && item.GetProperty("encrypted_content").GetString() == "opaque-1");
        foreach (var request in handler.Requests)
        {
            Assert.False(request.GetProperty("store").GetBoolean());
            Assert.False(request.TryGetProperty("previous_response_id", out _));
            Assert.False(request.TryGetProperty("conversation", out _));
            Assert.True(request.GetProperty("parallel_tool_calls").GetBoolean());
            var format = request.GetProperty("text").GetProperty("format");
            Assert.Equal("json_schema", format.GetProperty("type").GetString());
            Assert.False(format.GetProperty("strict").GetBoolean());
            Assert.Equal(16384, request.GetProperty("max_output_tokens").GetInt32());
            Assert.All(request.GetProperty("tools").EnumerateArray(), tool => Assert.Equal("function", tool.GetProperty("type").GetString()));
            if (modelName == "gpt-6.1-sol")
            {
                Assert.False(request.TryGetProperty("temperature", out _));
                Assert.False(request.TryGetProperty("top_p", out _));
                Assert.Equal("medium", request.GetProperty("reasoning").GetProperty("effort").GetString());
            }
        }
        var metrics = TelemetryReconciler.FromEvidence(session, TimeSpan.FromSeconds(1), false, null);
        Assert.True(metrics.TelemetryValid, string.Join("; ", metrics.TelemetryErrors));
        Assert.Equal(2, metrics.ModelRequestCount);
        Assert.Equal(20, metrics.InputTokens);
        Assert.Equal(4, metrics.CachedInputTokens);
        Assert.Equal(10, metrics.OutputTokens);
        Assert.Equal(6, metrics.ReasoningTokens);
        Assert.Equal(4, metrics.VisibleOutputTokens);
        Assert.Equal(30, metrics.TotalTokens);
        Assert.Equal(2, metrics.CacheWriteTokens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retry_accounting_preserves_budget_and_exhaustion_remains_infrastructure(bool exhaust)
    {
        using var session = new ExecutionSession(_root, "codeact", "small");
        using var handler = new ResponsesHandler("gpt-4o", "2024-11-20", throttle: true, exhaust: exhaust);
        using var http = new HttpClient(handler);
        using var client = CreateClient(http, "gpt-4o", "2024-11-20", invokeFunctions: false);
        var model = new ModelConfiguration { DeploymentName = "gpt-4o" };
        Task<ChatResponse> Send() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "Return JSON")],
            model.CreateChatOptions("Return JSON", [AIFunctionFactory.Create(() => "[]", "list_households")]));
        if (exhaust)
        {
            var error = await Assert.ThrowsAsync<ClientResultException>(Send);
            Assert.False(RunValidity.IsModelOutcome(DiagnosticChatClient.Describe(error.GetType().Name, error.Status, null)));
        }
        else
            await Send();
        var metrics = TelemetryReconciler.FromEvidence(session, TimeSpan.FromSeconds(5), false, null);
        Assert.True(metrics.TelemetryValid);
        Assert.Equal(1, metrics.ModelRequestCount);
        Assert.Equal(exhaust ? handler.Attempts : 1, metrics.Http429Count);
        Assert.Equal(handler.Attempts - 1, metrics.RetryCount);
        Assert.True(metrics.RetryWaitMs > 0);
        Assert.Equal(5000 - metrics.RetryWaitMs, metrics.Duration.TotalMilliseconds, 3);
    }

    [Fact]
    public async Task Observed_retry_policy_preserves_the_pinned_sdk_default_retry_limit()
    {
        using var baselineHandler = new ResponsesHandler("gpt-4o", "2024-11-20", throttle: true, exhaust: true);
        using var observedHandler = new ResponsesHandler("gpt-4o", "2024-11-20", throttle: true, exhaust: true);
        using var baselineHttp = new HttpClient(baselineHandler);
        using var observedHttp = new HttpClient(observedHandler);
        var sdk = new OpenAIClient(new ApiKeyCredential("test-key"), new OpenAIClientOptions
        {
            Endpoint = new Uri("https://responses.invalid/"), Transport = new HttpClientPipelineTransport(baselineHttp),
        });
        using var baseline = sdk.GetResponsesClient().AsIChatClient("gpt-4o");
        using var observed = CreateClient(observedHttp, "gpt-4o", "2024-11-20", invokeFunctions: false);
        await Assert.ThrowsAsync<ClientResultException>(() => baseline.GetResponseAsync([new ChatMessage(ChatRole.User, "test")]));
        var model = new ModelConfiguration { DeploymentName = "gpt-4o" };
        await Assert.ThrowsAsync<ClientResultException>(() => observed.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "test")],
            model.CreateChatOptions("test", [AIFunctionFactory.Create(() => "[]", "list_households")])));
        Assert.Equal(baselineHandler.Attempts, observedHandler.Attempts);
    }

    [Fact]
    public void Wrong_returned_snapshot_is_an_infrastructure_error()
    {
        var error = Assert.Throws<ModelRequestException>(() =>
            StatelessResponsesChatClient.VerifyModel("gpt-4o-2025-01-01", "gpt-4o", "2024-11-20"));
        Assert.Equal("DeploymentVersionMismatch", error.Failure.Category);
        Assert.False(RunValidity.IsModelOutcome(error.Failure));
    }

    [Fact]
    public void Pricing_uses_per_request_cache_and_long_context_rates_without_double_charging_reasoning()
    {
        var pricing = new PricingConfiguration
        {
            InputPerMillion = 2m, OutputPerMillion = 10m, CachedInputPerMillion = .1m, CacheWritePerMillion = 2.5m,
            LongContextThreshold = 272000, LongInputPerMillion = 4m, LongOutputPerMillion = 15m,
            LongCachedInputPerMillion = .2m, LongCacheWritePerMillion = 5m,
        };
        var usage = new ModelUsage[]
        {
            new(1, 200000, 100, 200100) { CachedInputTokens = 100000, ReasoningTokens = 80, CacheWriteTokens = 10000 },
            new(2, 200000, 100, 200100) { CachedInputTokens = 100000, ReasoningTokens = 80, CacheWriteTokens = 10000 },
        };
        Assert.Equal(.432m, pricing.EstimateCost(usage));
        Assert.Equal(.843m, pricing.EstimateCost(300000, 200, 100000, 20000));
        Assert.Null(pricing.EstimateCost(100, 10, null));
        Assert.NotEqual(pricing.Fingerprint(), (pricing with { CachedInputPerMillion = .2m }).Fingerprint());
        var config = new ModelConfiguration { DeploymentName = "d", ModelVersion = "1", ApiMode = "responses" };
        Assert.NotEqual(config.Fingerprint(), (config with { ModelVersion = "2" }).Fingerprint());
        Assert.NotEqual(config.Fingerprint(), (config with { DefaultReasoningEffort = "medium" }).Fingerprint());
    }

    private static IChatClient CreateClient(HttpClient http, string modelName, string version, bool invokeFunctions)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://responses.invalid/"), Transport = new HttpClientPipelineTransport(http),
            RetryPolicy = new FastRetryPolicy(),
        };
        options.AddPolicy(new AzureOpenAIChatClientProvider.ResponsesContractPolicy(), PipelinePosition.PerCall);
        var sdk = new OpenAIClient(new ApiKeyCredential("test-key"), options);
        var deployment = new AzureOpenAIOptions { ModelName = modelName, ModelVersion = version };
        IChatClient client = new ObservedChatClient(new StatelessResponsesChatClient(
            sdk.GetResponsesClient().AsIChatClient(modelName), deployment, _ => Task.CompletedTask), maxTurns: 2);
        return invokeFunctions ? new ObservedFunctionClient(client) : client;
    }

    private sealed class FastRetryPolicy : ObservedRetryPolicy
    {
        protected override TimeSpan GetNextDelay(PipelineMessage message, int tryCount) => TimeSpan.FromMilliseconds(1);
    }

    private sealed class ResponsesHandler(string modelName, string version, bool callFunction = false, bool throttle = false, bool exhaust = false) : HttpMessageHandler
    {
        public List<JsonElement> Requests { get; } = [];
        public int Attempts { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone());
            Attempts++;
            bool limited = throttle && (Attempts == 1 || exhaust);
            string output = callFunction && Attempts == 1
                ? """[{"type":"function_call","id":"fc1","call_id":"c1","name":"list_households","arguments":"{}","status":"completed"}]"""
                : """[{"type":"message","id":"msg1","role":"assistant","status":"completed","content":[{"type":"output_text","text":"{}","annotations":[]}]}]""";
            if (modelName == "gpt-6.1-sol")
                output = $$"""[{"type":"reasoning","id":"rs{{Attempts}}","summary":[],"encrypted_content":"opaque-{{Attempts}}"},""" + output[1..];
            string body = limited ? """{"error":{"code":"TooManyRequests","message":"rate limited"}}""" : $$$"""
                {"id":"resp{{{Attempts}}}","object":"response","created_at":0,"status":"completed","model":"{{{modelName}}}-{{{version}}}","output":{{{output}}},
                 "usage":{"input_tokens":10,"input_tokens_details":{"cached_tokens":2,"cache_write_tokens":1},
                  "output_tokens":5,"output_tokens_details":{"reasoning_tokens":3},"total_tokens":15}}
                """;
            var response = new HttpResponseMessage(limited ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            if (limited) response.Headers.Add("Retry-After", "0");
            return response;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
