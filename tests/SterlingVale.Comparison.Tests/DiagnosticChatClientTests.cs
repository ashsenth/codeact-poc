using Azure;
using Microsoft.Extensions.AI;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.AgentShared.Fakes;
using SterlingVale.Infrastructure.Model;
using Xunit;

namespace SterlingVale.Comparison.Tests;

public sealed class DiagnosticChatClientTests
{
    [Fact]
    public async Task Provider_failure_preserves_http_status_but_never_raw_server_text()
    {
        using var client = new DiagnosticChatClient(new FailingClient());
        var exception = await Assert.ThrowsAsync<ModelRequestException>(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "test")]));
        Assert.Equal(403, exception.Failure.HttpStatus);
        Assert.Equal("RequestFailedException", exception.Failure.Category);
        Assert.Contains("HTTP 403", exception.Failure.Message);
        Assert.Contains("Cognitive Services OpenAI User", exception.Failure.Message);
        Assert.DoesNotContain("secret-token", exception.Failure.Message);
        Assert.Equal("DeploymentNotFound", DiagnosticChatClient.Describe("ClientResultException", 404, "DeploymentNotFound").ErrorCode);
        Assert.Equal("UnrecognizedServiceError", DiagnosticChatClient.Describe("ClientResultException", 400, "secret-token").ErrorCode);
    }

    private sealed class FailingClient() : DelegatingChatClient(new DeterministicFakeChatClient(new FakeChatPlan().Respond("unused")))
    {
        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new RequestFailedException(403, "secret-token https://private.invalid", "Forbidden", null);
    }
}
