using System.Threading.Channels;

namespace VoceLocale.Core;

/// <summary>Groups adjacent speech segments briefly so spoken corrections stay in one block.</summary>
public static class SpeechBlockQueue
{
    public static async Task RunPreparedAsync<T>(ChannelReader<byte[]> reader, TimeSpan correctionWindow,
        Func<byte[], CancellationToken, Task<T>> prepare,
        Func<byte[], T, CancellationToken, Task> process, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(correctionWindow, TimeSpan.Zero);
        try
        {
            while (await reader.WaitToReadAsync(token))
            {
                if (!reader.TryRead(out var first)) continue;
                var parts = new List<byte[]> { first };
                Task<T>? prepared = null;
                byte[]? combined = null;
                try
                {
                    // ASR starts now; publication still waits for possible self-correction.
                    prepared = prepare(first, token);
                    using var window = CancellationTokenSource.CreateLinkedTokenSource(token);
                    window.CancelAfter(correctionWindow);
                    try
                    {
                        while (await reader.WaitToReadAsync(window.Token))
                        {
                            while (reader.TryRead(out var next)) parts.Add(next);
                            window.CancelAfter(correctionWindow);
                        }
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                    T result;
                    if (parts.Count == 1) result = await prepared;
                    else
                    {
                        // Complete the unused recognition before wiping its input. The combined
                        // audio is recognized once, preserving corrections across all adjacent parts.
                        try { await prepared; } catch when (!token.IsCancellationRequested) { }
                        token.ThrowIfCancellationRequested();
                        combined = new byte[parts.Sum(p => p.Length)];
                        int offset = 0;
                        foreach (var part in parts) { part.CopyTo(combined, offset); offset += part.Length; }
                        result = await prepare(combined, token);
                    }
                    await process(combined ?? first, result, token);
                }
                finally
                {
                    if (prepared != null) { try { await prepared; } catch { } }
                    foreach (var part in parts) Array.Clear(part);
                    if (combined != null) Array.Clear(combined);
                }
            }
        }
        finally { while (reader.TryRead(out var pending)) Array.Clear(pending); }
    }

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
