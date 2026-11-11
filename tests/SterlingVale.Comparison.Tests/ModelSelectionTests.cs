using SterlingVale.AgentShared.Configuration;
using SterlingVale.Benchmark;
using Xunit;

namespace SterlingVale.Comparison.Tests;

[Collection("environment")]
public sealed class ModelSelectionTests
{
    [Fact]
    public void Azure_environment_never_silently_switches_default_or_explicit_fake_to_live()
    {
        string? endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        try
        {
            Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", "https://example.invalid/");
            using var defaultMode = new Composition();
            using var fakeMode = new Composition("fake");
            Assert.False(defaultMode.Live);
            Assert.False(defaultMode.ChatClientProvider.IsLive);
            Assert.False(fakeMode.Live);
            Assert.Contains("default fake", ModelSelection.Parse(null).Reason);
            Assert.True(ModelSelection.Parse("live").IsLive);
            Assert.Throws<ArgumentException>(() => ModelSelection.Parse("automatic"));
            Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", null);
            Assert.Throws<InvalidOperationException>(() => new Composition("live"));
        }
        finally { Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", endpoint); }
    }
}
