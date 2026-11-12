using SterlingVale.Infrastructure.Model;
using SterlingVale.Infrastructure.Configuration;
using Xunit;

namespace SterlingVale.Comparison.Tests;

[Collection("environment")]
public sealed class EndpointNormalizationTests
{
    [Fact]
    public void Provider_announces_normalization_once_without_printing_endpoint()
    {
        var saved = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            _ = new AzureOpenAIChatClientProvider(new AzureOpenAIOptions
            {
                Endpoint = "https://example.invalid/openai/v1", DeploymentName = "demo", ApiKey = "test-key-not-real",
            });
        }
        finally { Console.SetOut(saved); }
        Assert.Equal("Azure OpenAI endpoint normalized to the base URL (path/query/fragment removed)." + Environment.NewLine, output.ToString());
        Assert.DoesNotContain("example.invalid", output.ToString());
    }

    [Theory]
    [InlineData("https://example.invalid/", false)]
    [InlineData("https://example.invalid/openai/v1", true)]
    [InlineData("https://example.invalid/openai/deployments/demo/chat/completions?api-version=example", true)]
    [InlineData("https://example.invalid/?example=true#fragment", true)]
    public void Normalizes_resource_url_without_leaking_or_changing_authority(string value, bool expectedNotice)
    {
        var endpoint = AzureOpenAIChatClientProvider.NormalizeEndpoint(value, out bool normalized);
        Assert.Equal("https://example.invalid/", endpoint.AbsoluteUri);
        Assert.Equal(expectedNotice, normalized);
    }

    [Fact]
    public void Rejects_insecure_invalid_and_credential_bearing_urls()
    {
        foreach (string value in new[] { "http://example.invalid/", "not-an-endpoint", "https://user:password@example.invalid/" })
            Assert.Throws<InvalidOperationException>(() => AzureOpenAIChatClientProvider.NormalizeEndpoint(value, out _));
    }
}
