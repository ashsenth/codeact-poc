using System.Text.Json;
using Microsoft.Extensions.AI;
using SterlingVale.Diagnostics;
using Xunit;

namespace SterlingVale.Comparison.Tests;

public sealed class FeasibilityControlTests
{
    [Fact]
    public async Task Control_schedules_only_the_supplied_program_without_estimated_model_usage()
    {
        const string program = "print(call_tool('list_households'))";
        using var client = new ReferenceProgramClient(program);
        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "ignored")]);
        var call = Assert.Single(response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>());
        Assert.Equal("execute_code", call.Name);
        Assert.Equal(program, call.Arguments!["code"]);
        Assert.Null(response.Usage);
    }

    [Fact]
    public async Task Control_echoes_execution_stdout_verbatim_without_filling_missing_components()
    {
        const string stdout = " {\"households\":[],\"status\":\"incomplete\",\"missingItems\":[\"test\"]}\n";
        using var client = new ReferenceProgramClient("never rerun");
        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("execute_code-0", JsonSerializer.Serialize(new { success = true, stdout }))])]);
        Assert.Equal(stdout, response.Text);
        Assert.Null(response.Usage);
        Assert.DoesNotContain(response.Messages.SelectMany(m => m.Contents), c => c is FunctionCallContent);
    }

    [Theory]
    [InlineData("{\"success\":false,\"stderr\":\"control failed\"}")]
    [InlineData("{\"success\":true,\"stdout\":null}")]
    public async Task Control_failure_or_absent_stdout_is_not_replaced_with_an_oracle_answer(string result)
    {
        using var client = new ReferenceProgramClient("never rerun");
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetResponseAsync(
            [new ChatMessage(ChatRole.Tool, [new FunctionResultContent("execute_code-0", result)])]));
    }
}
