using System.Threading.Channels;
using VoceLocale.Core;

internal static class SpeechPipelineChecks
{
    public static async Task RunAsync(Action<string, bool> check)
    {
        var queue = Channel.CreateUnbounded<byte[]>();
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int recognized = 0, published = 0; byte[] input = [1, 2, 3];
        var run = SpeechBlockQueue.RunPreparedAsync(queue.Reader, TimeSpan.FromMilliseconds(200), async (audio, token) =>
        { Interlocked.Increment(ref recognized); prepared.TrySetResult(); await Task.Delay(50, token); return audio.Sum(x => (int)x); },
        (audio, result, token) => { published++; check("prepared recognition result belongs to the exact audio", result == 6); return Task.CompletedTask; }, CancellationToken.None);
        queue.Writer.TryWrite(input); await prepared.Task.WaitAsync(TimeSpan.FromSeconds(2));
        check("recognition starts while the correction window remains open", published == 0);
        queue.Writer.TryComplete(); await run;
        check("an unchanged phrase is recognized and inserted only once", recognized == 1 && published == 1);
        check("prepared audio is wiped after publication", input.All(x => x == 0));

        var joined = Channel.CreateUnbounded<byte[]>(); var seen = new List<string>();
        byte[] a = [1], b = [2], c = [3];
        var joinRun = SpeechBlockQueue.RunPreparedAsync(joined.Reader, TimeSpan.FromMilliseconds(100),
            (audio, token) => Task.FromResult(string.Join(',', audio)),
            (audio, result, token) => { seen.Add(result); return Task.CompletedTask; }, CancellationToken.None);
        joined.Writer.TryWrite(a); await Task.Delay(20); joined.Writer.TryWrite(b); await Task.Delay(20); joined.Writer.TryWrite(c); joined.Writer.TryComplete(); await joinRun;
        check("all successive corrections are recognized as one confirmed block", seen.SequenceEqual(new[] { "1,2,3" }));
        check("merged preparation clears all source buffers", a[0] == 0 && b[0] == 0 && c[0] == 0);

        var cancelling = Channel.CreateUnbounded<byte[]>(); using var cancel = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); byte[] discarded = [9];
        var cancellationRun = SpeechBlockQueue.RunPreparedAsync(cancelling.Reader, TimeSpan.FromSeconds(1),
            async (audio, token) => { started.TrySetResult(); await Task.Delay(10000, token); return 0; },
            (_, _, _) => throw new Exception("Cancelled audio must not publish."), cancel.Token);
        cancelling.Writer.TryWrite(discarded); await started.Task; cancel.Cancel();
        try { await cancellationRun; } catch (OperationCanceledException) { }
        check("cancelled speculative recognition never publishes and clears audio", discarded[0] == 0);
    }
}
