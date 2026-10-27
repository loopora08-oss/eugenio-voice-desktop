using System.Threading.Channels;

namespace VoceLocale.Core;

/// <summary>Bounded audio queue; publishes original captions before waiting for translation.</summary>
public sealed class MeetingStream : IDisposable
{
    private sealed record Job(MeetingAudioChunk Chunk, bool Translate);
    // Text only: roughly 1 MB per hour of ordinary speech, independent of audio storage.
    private readonly Channel<MeetingEntry> _translationQueue = Channel.CreateBounded<MeetingEntry>(4096);
    private readonly Channel<Job> _queue;
    private readonly List<Job> _overflow = [];
    private readonly List<MeetingEntry> _entries = [];
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancel = new();
    private readonly Func<MeetingAudioChunk, CancellationToken, Task<EngineResponse>> _transcribe;
    private readonly Func<string, string?, CancellationToken, Task<string?>> _translate;
    private readonly Task _worker;
    private readonly Task _translationWorker;
    private bool _completed, _overloaded, _disposed;
    private readonly Dictionary<MeetingAudioSource, string> _previous = new();
    public event Action<MeetingEntry>? EntryAdded;
    public event Action<MeetingEntry>? EntryUpdated;
    public event Action<string>? Fault;
    public event Action<int>? QueueChanged;
    public event Action<int>? TranslationQueueChanged;
    public int PendingChunks { get; private set; }
    public int PendingTranslations { get; private set; }
    public Task TranslationCompletion => _translationWorker;
    public IReadOnlyList<MeetingEntry> Snapshot() { lock (_gate) return _entries.ToArray(); }

    public MeetingStream(Func<MeetingAudioChunk, CancellationToken, Task<EngineResponse>> transcribe,
        Func<string, CancellationToken, Task<string?>> translate, int capacity = 24)
        : this(transcribe, (text, language, token) => translate(text, token), capacity) { }
    public MeetingStream(Func<MeetingAudioChunk, CancellationToken, Task<EngineResponse>> transcribe,
        Func<string, string?, CancellationToken, Task<string?>> translate, int capacity = 24)
    {
        _transcribe = transcribe; _translate = translate;
        _queue = Channel.CreateBounded<Job>(new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
        _worker = RunAsync();
        _translationWorker = TranslateAsync();
    }
    public bool Submit(MeetingAudioChunk chunk, bool translate)
    {
        bool accepted;
        lock (_gate)
        {
            if (_completed || _disposed) { Array.Clear(chunk.Audio); return false; }
            var job = new Job(chunk, translate);
            accepted = !_overloaded && _queue.Writer.TryWrite(job);
            if (!accepted)
            {
                _overloaded = true;
                if (_overflow.Count >= 4) { Array.Clear(chunk.Audio); throw new InvalidOperationException("Capture did not stop after backpressure."); }
                _overflow.Add(job);
            }
            PendingChunks++;
        }
        QueueChanged?.Invoke(PendingChunks);
        // false tells the capture owner to stop immediately, preserving the overflow tail.
        return accepted;
    }
    public async Task CompleteAsync()
    {
        await CompleteCaptureAsync().ConfigureAwait(false);
        await _translationWorker.ConfigureAwait(false);
    }
    public async Task CompleteCaptureAsync()
    {
        lock (_gate) { _completed = true; _queue.Writer.TryComplete(); }
        await _worker.ConfigureAwait(false);
        _translationQueue.Writer.TryComplete();
    }
    private async Task RunAsync()
    {
        try
        {
            await foreach (var job in _queue.Reader.ReadAllAsync(_cancel.Token)) await ProcessAsync(job).ConfigureAwait(false);
            Job[] overflow; lock (_gate) { overflow = _overflow.ToArray(); _overflow.Clear(); }
            foreach (var job in overflow) await ProcessAsync(job).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        finally
        {
            while (_queue.Reader.TryRead(out var job)) Array.Clear(job.Chunk.Audio);
            lock (_gate) { foreach (var job in _overflow) Array.Clear(job.Chunk.Audio); _overflow.Clear(); PendingChunks = 0; }
        }
    }
    private async Task ProcessAsync(Job job)
    {
        var chunk = job.Chunk;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            var result = await _transcribe(chunk, timeout.Token).ConfigureAwait(false);
            if (!result.Ok)
            {
                if (result.Error == "silence") return;
                throw new IOException("Transcription failed.");
            }
            string text = MeetingText.RemoveOverlap(_previous.GetValueOrDefault(chunk.Source, ""), result.Text ?? "", chunk.Overlap);
            if (text.Length == 0) return;
            _previous[chunk.Source] = result.Text ?? text;
            var entry = new MeetingEntry(chunk.Sequence, chunk.StartSeconds, chunk.EndSeconds, text, TranslationRequested: job.Translate, Language: result.Language, Source: chunk.Source,
                Provenance: result.Provenance);
            lock (_gate) _entries.Add(entry);
            EntryAdded?.Invoke(entry);
            if (job.Translate)
            {
                bool accepted;
                lock (_gate) { PendingTranslations++; accepted = _translationQueue.Writer.TryWrite(entry); if (!accepted) PendingTranslations--; }
                if (!accepted) UpdateTranslation(entry, null, "Traduzione in ritardo: originale conservato, blocco non tradotto.");
                TranslationQueueChanged?.Invoke(PendingTranslations);
            }
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { }
        catch
        {
            var missing = new MeetingEntry(chunk.Sequence, chunk.StartSeconds, chunk.EndSeconds, "", Warning: "Blocco non trascritto: intervallo da verificare.", Source: chunk.Source);
            lock (_gate) _entries.Add(missing);
            EntryAdded?.Invoke(missing);
            Fault?.Invoke("Trascrizione interrotta. I blocchi già completati restano nel notetaker.");
        }
        finally
        {
            Array.Clear(chunk.Audio);
            lock (_gate) PendingChunks--;
            QueueChanged?.Invoke(PendingChunks);
        }
    }
    private async Task TranslateAsync()
    {
        try
        {
            await foreach (var entry in _translationQueue.Reader.ReadAllAsync(_cancel.Token))
            {
                string? english = null;
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(90));
                    var candidate = await _translate(entry.Original, entry.Language, timeout.Token).ConfigureAwait(false);
                    if (TranslationPolicy.TryValidate(entry.Original, candidate, null, out var validated)) english = validated;
                }
                catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { throw; }
                catch { }
                UpdateTranslation(entry, english, english == null ? "Traduzione non validata: conserva e verifica l'originale." : null);
                lock (_gate) PendingTranslations--;
                TranslationQueueChanged?.Invoke(PendingTranslations);
            }
        }
        catch (OperationCanceledException) { }
        finally { while (_translationQueue.Reader.TryRead(out _)) { } lock (_gate) PendingTranslations = 0; }
    }
    private void UpdateTranslation(MeetingEntry entry, string? english, string? warning)
    {
        entry = entry with { English = english, Warning = warning };
        lock (_gate) _entries[_entries.FindIndex(x => x.Sequence == entry.Sequence)] = entry;
        EntryUpdated?.Invoke(entry);
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _cancel.Cancel(); _queue.Writer.TryComplete(); _translationQueue.Writer.TryComplete();
        // Cancel callbacks can still be unwinding; the CTS stays alive until the worker exits.
        _ = Task.WhenAll(_worker, _translationWorker).ContinueWith(_ => _cancel.Dispose(), TaskScheduler.Default);
    }
}
