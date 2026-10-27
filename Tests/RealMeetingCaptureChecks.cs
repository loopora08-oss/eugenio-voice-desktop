using System.Diagnostics;
using System.Text.Json;
using VoceLocale;
using VoceLocale.Core;

internal static class RealMeetingCaptureChecks
{
    public static async Task<int> RunAsync(string report)
    {
        int levels=0, failures=0, microphoneChunks=0, systemChunks=0;
        bool stoppedEvents=false, passed=false;
        string? error=null;
        var time=Stopwatch.StartNew();
        try
        {
            using var capture=new MeetingRecorder(includeSystemAudio:true);
            capture.InputLevelChanged+=_=>Interlocked.Increment(ref levels);
            capture.DeviceFailed+=()=>Interlocked.Increment(ref failures);
            capture.ChunkReady+=chunk=>
            {
                if(chunk.Source==MeetingAudioSource.System) Interlocked.Increment(ref systemChunks);
                else Interlocked.Increment(ref microphoneChunks);
                Array.Clear(chunk.Audio);
            };
            capture.Start(0);
            await Task.Delay(2000);
            await capture.StopAsync();
            int pausedLevels=levels;
            await Task.Delay(300);
            bool paused=levels==pausedLevels;
            capture.Start(0);
            await Task.Delay(2000);
            await Task.WhenAll(capture.StopAsync(),capture.StopAsync());
            int finalLevels=levels;
            await Task.Delay(300);
            stoppedEvents=levels==finalLevels;
            passed=levels>0&&failures==0&&paused&&stoppedEvents&&capture.CapturedSeconds>=4;
        }
        catch(Exception exception) { error=exception.GetType().Name; }
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{realHardware=true,passed,error,levels,failures,microphoneChunks,systemChunks,
            stoppedEvents,elapsedSeconds=time.Elapsed.TotalSeconds,audioSaved=false,transcriptSaved=false,
            speechAccuracyProven=false,systemAudioActivityObserved=systemChunks>0,systemSpeechCaptureProven=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(new{realHardware=true,passed,error,levels,failures,microphoneChunks,systemChunks,stoppedEvents}));
        return passed?0:1;
    }
}
