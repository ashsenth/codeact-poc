using System.ClientModel.Primitives;
using System.Diagnostics;
using SterlingVale.Telemetry;

namespace SterlingVale.Infrastructure.Model;

public class ObservedRetryPolicy : ClientRetryPolicy
{
    protected override void OnRequestSent(PipelineMessage message)
    {
        base.OnRequestSent(message);
        if (message.Response?.Status == 429)
            ExecutionSession.Current?.Emit("MODEL_HTTP_429", "retryable");
    }

    protected override async ValueTask OnRequestSentAsync(PipelineMessage message)
    {
        await base.OnRequestSentAsync(message).ConfigureAwait(false);
        if (message.Response?.Status == 429)
            ExecutionSession.Current?.Emit("MODEL_HTTP_429", "retryable");
    }

    protected override async Task WaitAsync(TimeSpan time, CancellationToken cancellationToken)
    {
        string waitId = Guid.NewGuid().ToString("N");
        var session = ExecutionSession.Current;
        session?.Emit("MODEL_RETRY_WAIT_STARTED", data: new { invocationId = waitId, requestedMs = time.TotalMilliseconds });
        long start = Stopwatch.GetTimestamp();
        try
        {
            await base.WaitAsync(time, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            session?.Emit("MODEL_RETRY_WAIT_COMPLETED", durationMs: Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                data: new { invocationId = waitId });
        }
    }

    protected override void Wait(TimeSpan time, CancellationToken cancellationToken)
    {
        string waitId = Guid.NewGuid().ToString("N");
        var session = ExecutionSession.Current;
        session?.Emit("MODEL_RETRY_WAIT_STARTED", data: new { invocationId = waitId, requestedMs = time.TotalMilliseconds });
        long start = Stopwatch.GetTimestamp();
        try
        {
            base.Wait(time, cancellationToken);
        }
        finally
        {
            session?.Emit("MODEL_RETRY_WAIT_COMPLETED", durationMs: Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                data: new { invocationId = waitId });
        }
    }
}
