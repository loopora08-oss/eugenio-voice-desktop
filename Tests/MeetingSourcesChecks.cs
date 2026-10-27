using System.Text.Json;
using NAudio.Wave;
using VoceLocale;
using VoceLocale.Core;

internal static class MeetingSourcesChecks
{
    public static async Task RunAsync(Action<string, bool> check)
    {
        byte[] tone = Tone(); var chunks = new List<MeetingAudioChunk>();
        using (var timeline = new MeetingSourceTimeline(true))
        {
            timeline.ChunkReady += chunks.Add;
            timeline.Push(MeetingAudioSource.Microphone, tone, TimeSpan.FromSeconds(.5));
            timeline.Push(MeetingAudioSource.System, tone, TimeSpan.FromSeconds(10.5));
            timeline.Flush();
            check("le sorgenti meeting hanno sequenze globali distinte", chunks.Select(c => c.Sequence).Distinct().Count() == chunks.Count && chunks.Count == 2);
            check("il silenzio iniziale del PC non sposta la sua voce al minuto zero", chunks.Single(c => c.Source == MeetingAudioSource.System).StartSeconds >= 10);
            check("microfono e PC mantengono separati i buffer", !ReferenceEquals(chunks[0].Audio, chunks[1].Audio));
            timeline.AdvanceTo(TimeSpan.FromHours(2));
            timeline.Push(MeetingAudioSource.Microphone, tone, TimeSpan.FromHours(2) + TimeSpan.FromSeconds(.5));
            timeline.Flush();
            check("pausa lunga senza buffer audio proporzionale alla pausa", chunks.Last().StartSeconds >= 7200 && chunks.Last().Audio.Length <= MeetingAudioSegmenter.MaxChunkBytes);
        }
        foreach (var chunk in chunks) Array.Clear(chunk.Audio);

        bool disabled = false, odd = false, negative = false;
        using (var microphoneOnly = new MeetingSourceTimeline(false))
        {
            try { microphoneOnly.Push(MeetingAudioSource.System, tone, TimeSpan.FromSeconds(1)); } catch (ArgumentOutOfRangeException) { disabled = true; }
            try { microphoneOnly.Push(MeetingAudioSource.Microphone, [1], TimeSpan.FromSeconds(1)); } catch (ArgumentException) { odd = true; }
            try { microphoneOnly.AdvanceTo(TimeSpan.FromSeconds(-1)); } catch (ArgumentOutOfRangeException) { negative = true; }
        }
        check("una sorgente non autorizzata viene rifiutata", disabled);
        check("frame incompleti e tempo negativo vengono rifiutati", odd && negative);

        var entries = new List<MeetingEntry>();
        using (var stream = new MeetingStream((chunk, _) => Task.FromResult(new EngineResponse("test", true,
            chunk.Sequence switch { 1 => "Uno due tre", 2 => "tre quattro", _ => "tre cinque" })), (_, _) => Task.FromResult<string?>(null)))
        {
            stream.EntryAdded += entries.Add;
            stream.Submit(new(1, 0, 8000, tone.ToArray(), false, MeetingAudioSource.Microphone), false);
            stream.Submit(new(2, 0, 8000, tone.ToArray(), true, MeetingAudioSource.System), false);
            stream.Submit(new(3, 8000, 16000, tone.ToArray(), true, MeetingAudioSource.Microphone), false);
            await stream.CompleteAsync();
            check("l'overlap non cancella parole tra persone su sorgenti differenti", entries[1].Original == "tre quattro" && entries[2].Original == "cinque");
            check("la sorgente resta associata alla trascrizione finale", entries[0].Source == MeetingAudioSource.Microphone && entries[1].Source == MeetingAudioSource.System);
        }
        var document = new MeetingDocument { Entries = entries };
        var restored = JsonSerializer.Deserialize<MeetingDocument>(JsonSerializer.Serialize(document))!;
        check("salvataggio e riapertura conservano la sorgente", restored.IsValid() && restored.Entries[1].Source == MeetingAudioSource.System);
        check("Markdown distingue PC e microfono senza inventare nomi", document.Markdown().Contains("Audio PC · parlante non identificato") && document.Markdown().Contains("Microfono:"));
        check("una sorgente importata sconosciuta non è accettata", !new MeetingDocument { Entries = [new(1, 0, 1, "Testo", Source: (MeetingAudioSource)99)] }.IsValid());

        var microphone = new FakeWaveIn(); var system = new FakeWaveIn();
        using (var recorder = new MeetingRecorder(true, _ => microphone, () => system))
        {
            recorder.Start(0);
            bool duplicateRejected = false;
            try { recorder.Start(0); } catch (InvalidOperationException) { duplicateRejected = true; }
            check("doppio avvio rifiutato senza spegnere la cattura in corso", duplicateRejected && microphone.StartCount == 1 && !microphone.Disposed);
            byte[] input = tone.ToArray(); microphone.Emit(input);
            check("il callback di cattura azzera il buffer ricevuto", input.All(b => b == 0));
            await Task.WhenAll(recorder.StopAsync(), recorder.StopAsync());
            check("doppio stop arresta entrambe le sorgenti una sola volta", microphone.StopCount == 1 && system.StopCount == 1 && microphone.Disposed && system.Disposed);
        }
        var partial = new FakeWaveIn(); var unavailable = new FakeWaveIn { FailOnStart = true };
        bool startupFailed = false;
        using (var recorder = new MeetingRecorder(true, _ => partial, () => unavailable))
        {
            try { recorder.Start(0); } catch (IOException) { startupFailed = true; }
        }
        check("un guasto all'avvio del PC libera anche il microfono già aperto", startupFailed && partial.Disposed && unavailable.Disposed);
        var disconnected = new FakeWaveIn(); int faults = 0;
        using (var recorder = new MeetingRecorder(false, _ => disconnected))
        {
            recorder.DeviceFailed += () => faults++;
            recorder.Start(0); disconnected.Disconnect(); await recorder.StopAsync();
        }
        check("una sorgente scollegata produce un errore esplicito", faults == 1);
        Array.Clear(tone);
    }
    private static byte[] Tone()
    {
        var pcm = new byte[16000];
        for (int i = 0; i < pcm.Length / 2; i++)
        {
            short sample = (short)(2000 * Math.Sin(2 * Math.PI * 220 * i / 16000));
            pcm[2 * i] = (byte)sample; pcm[2 * i + 1] = (byte)(sample >> 8);
        }
        return pcm;
    }
    private sealed class FakeWaveIn : IWaveIn
    {
        public WaveFormat WaveFormat { get; set; } = new(16000, 16, 1);
        public event EventHandler<WaveInEventArgs>? DataAvailable;
        public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public int StartCount, StopCount;
        public bool FailOnStart, Disposed;
        public void StartRecording() { if (FailOnStart) throw new IOException("Sorgente non disponibile."); StartCount++; }
        public void StopRecording() { StopCount++; RecordingStopped?.Invoke(this, new StoppedEventArgs()); }
        public void Emit(byte[] input) => DataAvailable?.Invoke(this, new WaveInEventArgs(input, input.Length));
        public void Disconnect() => RecordingStopped?.Invoke(this, new StoppedEventArgs(new IOException("Dispositivo scollegato.")));
        public void Dispose() => Disposed = true;
    }
}
