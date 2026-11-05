using SterlingVale.Telemetry;
using Xunit;

namespace SterlingVale.Tools.Tests;

public sealed class PayloadPolicyTests
{
    [Fact]
    public void Metadata_is_default_and_does_not_capture_content()
    {
        var captured = new PayloadPolicy().Capture(new { value = "synthetic" });
        Assert.Null(captured.Content);
        Assert.True(captured.OriginalLength > 0);
        Assert.Equal("metadata", captured.CaptureMode);
    }

    [Fact]
    public void Full_capture_redacts_nested_secrets_and_bearer_values()
    {
        var captured = new PayloadPolicy(PayloadCaptureMode.Full).Capture(new { nested = new { apiKey = "private-key" }, message = "Bearer private-token" });
        Assert.True(captured.Redacted);
        Assert.DoesNotContain("private-key", captured.Content);
        Assert.DoesNotContain("private-token", captured.Content);
        Assert.Contains("REDACTED", captured.Content);
    }

    [Fact]
    public void Full_capture_is_bounded_and_marks_truncation()
    {
        var captured = new PayloadPolicy(PayloadCaptureMode.Full, 10).Capture(new string('x', 30));
        Assert.Equal(10, captured.Content!.Length);
        Assert.True(captured.Truncated);
        Assert.Equal(30, captured.OriginalLength);
    }

    [Fact]
    public void Full_capture_does_not_reformat_json_shaped_python_or_model_json()
    {
        const string program = "[1,  2, 3]\n";
        Assert.Equal(program, new PayloadPolicy(PayloadCaptureMode.Full).Capture(program).Content);
    }
}