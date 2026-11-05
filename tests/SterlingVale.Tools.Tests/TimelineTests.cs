using System.Text.Json;
using SterlingVale.Telemetry;
using Xunit;

namespace SterlingVale.Tools.Tests;

public sealed class TimelineTests
{
    [Fact]
    public void Disposal_finalizes_real_events_with_back_references_even_after_failure()
    {
        string root = Path.Combine(Path.GetTempPath(), "sv-timeline-" + Guid.NewGuid().ToString("N"));
        try
        {
            string directory;
            string eventId;
            using (var session = new ExecutionSession(root, "codeact", "small"))
            {
                directory = session.RunDirectory;
                eventId = session.Emit("RUN_FAILED", "error", errorCategory: "TimeoutException").EventId;
            }

            using var timeline = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "timeline.json")));
            Assert.Equal(1, timeline.RootElement.GetArrayLength());
            Assert.Contains(eventId, timeline.RootElement[0].GetProperty("eventReference").GetString());
            Assert.Contains("RUN_FAILED", File.ReadAllText(Path.Combine(directory, "timeline.md")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}