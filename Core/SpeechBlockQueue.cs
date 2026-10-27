using System.Threading.Channels;
using System.Diagnostics;

namespace VoceLocale.Core;

/// <summary>Raggruppa segmenti vicini preservando i ripensamenti e la pulizia dei buffer.</summary>
public static class SpeechBlockQueue
{
    public static async Task RunPreparedAsync<T>(ChannelReader<byte[]> reader, TimeSpan correctionWindow,
        Func<byte[], CancellationToken, Task<T>> prepare,
        Func<byte[], T, CancellationToken, Task> process, CancellationToken token, Func<bool>? hasPendingSpeech = null,
        Func<IReadOnlyList<T>, T>? combinePrepared = null, Action<SpeechBlockMetrics>? measured = null,
        int maximumAudioBytes = 16000 * 2 * 120)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(correctionWindow, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumAudioBytes, 0);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(prepare);
        ArgumentNullException.ThrowIfNull(process);
        try
        {
            while (await reader.WaitToReadAsync(token))
            {
                if (!reader.TryRead(out var first)) continue;
                var parts = new List<byte[]> { first };
                var recognitions = new List<Task<T>>();
                using var preparationLifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
                var watch = Stopwatch.StartNew();
                Task<T>? prepared = null;
                byte[]? combined = null;
                int totalBytes = first.Length, recognitionPasses = 0;
                try
                {
                    if (totalBytes > maximumAudioBytes) throw new InvalidDataException("Blocco vocale troppo lungo. Ferma la dettatura e riprova con una frase più breve.");
                    // Il riconoscimento anticipa la conferma, ma nessuna ipotesi viene digitata.
                    prepared = prepare(first, preparationLifetime.Token);
                    recognitions.Add(prepared); recognitionPasses++;
                    bool waiting = true;
                    while (waiting)
                    {
                        using var window = CancellationTokenSource.CreateLinkedTokenSource(token);
                        window.CancelAfter(correctionWindow);
                        try
                        {
                            if (await reader.WaitToReadAsync(window.Token))
                                while (reader.TryRead(out var next))
                                {
                                    parts.Add(next);
                                    totalBytes = checked(totalBytes + next.Length);
                                    if (parts.Count > 256 || totalBytes > maximumAudioBytes)
                                        throw new InvalidDataException("Troppi segmenti in attesa. Ferma la dettatura e riprova con una frase più breve.");
                                    if (combinePrepared != null)
                                    {
                                        recognitions.Add(prepare(next, preparationLifetime.Token));
                                        recognitionPasses++;
                                    }
                                }
                            else waiting = false;
                        }
                        catch (OperationCanceledException) when (!token.IsCancellationRequested)
                        { waiting = hasPendingSpeech?.Invoke() == true; }
                    }
                    double confirmationMs = watch.Elapsed.TotalMilliseconds;
                    T result;
                    if (parts.Count == 1) result = await prepared;
                    else if (combinePrepared != null)
                    {
                        // Ogni segmento è riconosciuto una volta. La correzione finale riceve
                        // tutto il testo, incluse negazioni e ripensamenti tra segmenti.
                        result = combinePrepared(await Task.WhenAll(recognitions));
                        combined = new byte[totalBytes];
                        int offset = 0;
                        foreach (var part in parts) { part.CopyTo(combined, offset); offset += part.Length; }
                    }
                    else
                    {
                        // Complete the unused recognition before wiping its input. The combined
                        // audio is recognized once, preserving corrections across all adjacent parts.
                        try { await prepared; } catch when (!token.IsCancellationRequested) { }
                        token.ThrowIfCancellationRequested();
                        combined = new byte[totalBytes];
                        int offset = 0;
                        foreach (var part in parts) { part.CopyTo(combined, offset); offset += part.Length; }
                        recognitionPasses++;
                        result = await prepare(combined, preparationLifetime.Token);
                    }
                    double preparedMs = watch.Elapsed.TotalMilliseconds;
                    token.ThrowIfCancellationRequested();
                    await process(combined ?? first, result, token);
                    // L'osservatore riceve esclusivamente tempi e contatori, mai contenuti.
                    if (measured != null)
                    {
                        var metric = new SpeechBlockMetrics(Guid.NewGuid(), parts.Count, recognitionPasses,
                            totalBytes, confirmationMs, preparedMs, watch.Elapsed.TotalMilliseconds);
                        try { measured(metric); } catch { /* La diagnostica non deve duplicare un inserimento già riuscito. */ }
                    }
                }
                finally
                {
                    preparationLifetime.Cancel();
                    foreach (var recognition in recognitions) { try { await recognition; } catch { } }
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
