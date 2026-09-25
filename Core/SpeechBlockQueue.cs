using System.Threading.Channels;

namespace VoceLocale.Core;

/// <summary>Groups adjacent speech segments briefly so spoken corrections stay in one block.</summary>
public static class SpeechBlockQueue
{
    public static async Task RunAsync(ChannelReader<byte[]> reader, TimeSpan correctionWindow,
        Func<byte[], CancellationToken, Task> process, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(correctionWindow, TimeSpan.Zero);
        byte[]? carry = null;
        try
        {
            while (await reader.WaitToReadAsync(token))
            {
                if (!reader.TryRead(out var segment)) continue;
                if (carry == null)
                {
                    carry = segment;
                    using var window = CancellationTokenSource.CreateLinkedTokenSource(token);
                    window.CancelAfter(correctionWindow);
                    try
                    {
                        if (await reader.WaitToReadAsync(window.Token)) continue;
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                    try { await process(carry, token); }
                    finally { Array.Clear(carry); carry = null; }
                    continue;
                }

                byte[]? block = null;
                try
                {
                    block = new byte[carry.Length + segment.Length];
                    carry.CopyTo(block, 0);
                    segment.CopyTo(block, carry.Length);
                    Array.Clear(carry); Array.Clear(segment); carry = null;
                    await process(block, token);
                }
                finally
                {
                    if (carry != null) { Array.Clear(carry); carry = null; }
                    Array.Clear(segment);
                    if (block != null) Array.Clear(block);
                }
            }
            if (carry != null)
            {
                try { await process(carry, token); }
                finally { Array.Clear(carry); carry = null; }
            }
        }
        finally
        {
            if (carry != null) Array.Clear(carry);
            while (reader.TryRead(out var queued)) Array.Clear(queued);
        }
    }
}
