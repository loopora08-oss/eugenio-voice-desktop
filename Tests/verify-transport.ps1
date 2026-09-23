param([Parameter(Mandatory=$true)][string]$AppDirectory)
$ErrorActionPreference='Stop'
# This script does not open a microphone. It checks crash containment using the
# already-built worker and an intentionally absent model path.
$start=[System.Diagnostics.ProcessStartInfo]::new((Join-Path $AppDirectory 'engines/RewriteWorker/RewriteWorker.exe'))
$start.UseShellExecute=$false
$start.CreateNoWindow=$true
$start.RedirectStandardInput=$true
$start.RedirectStandardOutput=$true
$start.RedirectStandardError=$true
$process=[System.Diagnostics.Process]::Start($start)
$stderr=$process.StandardError.ReadToEndAsync()
try {
    $request=@{Id='missing-model';Operation='load';ModelPath=(Join-Path $AppDirectory 'data/models/absent-model.gguf')} | ConvertTo-Json -Compress
    $process.StandardInput.WriteLine($request)
    $process.StandardInput.Flush()
    $reply=$null
    $deadline=[DateTime]::UtcNow.AddSeconds(10)
    while([DateTime]::UtcNow -lt $deadline) {
        $line=$process.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(10)).GetAwaiter().GetResult()
        if($null -eq $line) { throw 'Worker exited without protocol response' }
        if($line.StartsWith('{')) {
            $candidate=$line | ConvertFrom-Json
            if($candidate.Id -eq 'missing-model') { $reply=$candidate; break }
        }
    }
    if($null -eq $reply -or $reply.Ok -or $reply.Error -ne 'engine') { throw 'Missing model was not contained' }
    'PASS missing model produces a bounded error, no traceback or content'
} finally {
    $process.StandardInput.Close()
    if(!$process.WaitForExit(3000)) { $process.Kill($true) }
    $process.Dispose()
}
