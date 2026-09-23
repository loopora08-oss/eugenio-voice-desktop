param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$speaker = New-Object -ComObject SAPI.SpVoice
$voices = $speaker.GetVoices()
function Write-Speech([string]$Name, [string]$Language, [string]$Text) {
    $voice = @($voices | Where-Object { $_.GetAttribute('Language') -match $Language }) | Select-Object -First 1
    if (!$voice) { throw "No local voice for $Language" }
    $speaker.Voice = $voice
    $stream = New-Object -ComObject SAPI.SpFileStream
    $stream.Format.Type = 22 # SAFT16kHz16BitMono
    $file = Join-Path $OutputDirectory $Name
    $stream.Open($file,3,$false)
    $speaker.AudioOutputStream=$stream
    [void]$speaker.Speak($Text)
    $stream.Close()
}
Write-Speech 'it-correction.wav' '410' 'Ci vediamo martedì, no, intendevo giovedì alle tre. Non voglio annullare la prenotazione.'
Write-Speech 'en-correction.wav' '409' 'Let us meet on Tuesday, no, Thursday at three. I do not want to cancel the reservation.'
$paragraphs = @(
'Sto preparando il programma della prossima settimana. Lunedì lavoreremo alla presentazione e controlleremo i documenti. La riunione sarà martedì, no, intendevo giovedì alle tre del pomeriggio.',
'Per favore manda il documento a Marco, anzi a Luca. Non voglio annullare la prenotazione. Il costo previsto è di cinquanta, scusa, quindici euro per persona.',
'Prima di partire dobbiamo verificare gli orari del treno e prenotare una camera vicino alla stazione. Porteremo il computer e tutti gli appunti necessari per il nostro incontro.',
'Il progetto comprende una prima fase di ricerca e una seconda fase di verifica. Ogni risultato dovrà essere discusso con il gruppo prima della consegna finale.',
'La versione aggiornata contiene tutte le modifiche richieste. Ho corretto i numeri della tabella e aggiunto una spiegazione dei passaggi più importanti.',
'Domani mattina controlleremo la qualità del lavoro. Se manca qualche informazione, scriveremo una nota per ricordare il punto da chiarire durante la telefonata.',
'Nel pomeriggio vorrei organizzare i materiali in modo semplice. Una cartella conterrà i documenti approvati e una seconda cartella raccoglierà le bozze ancora da rivedere.'
)
Write-Speech 'benchmark-source.wav' '410' (($paragraphs + $paragraphs + $paragraphs) -join ' ')
$source = Join-Path $OutputDirectory 'benchmark-source.wav'
$reader=[System.IO.BinaryReader]::new([System.IO.File]::OpenRead($source))
[void]$reader.ReadBytes(12)
$pcm=$null
while($reader.BaseStream.Position + 8 -le $reader.BaseStream.Length) {
    $id=[System.Text.Encoding]::ASCII.GetString($reader.ReadBytes(4)); $length=$reader.ReadInt32()
    if($id -eq 'data') { $pcm=$reader.ReadBytes($length); break }
    [void]$reader.BaseStream.Seek($length + ($length % 2),[System.IO.SeekOrigin]::Current)
}
$reader.Dispose()
foreach($seconds in @(10,30,120)) {
    $bytes=32000*$seconds
    if($pcm.Length -lt $bytes) { throw 'Synthetic source too short' }
    $writer=[System.IO.BinaryWriter]::new([System.IO.File]::Create((Join-Path $OutputDirectory "benchmark-$seconds.wav")))
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write([int](36+$bytes)); $writer.Write([System.Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write([int]16)
    $writer.Write([int16]1); $writer.Write([int16]1); $writer.Write([int]16000); $writer.Write([int]32000); $writer.Write([int16]2); $writer.Write([int16]16)
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes('data')); $writer.Write([int]$bytes); $writer.Write($pcm,0,$bytes); $writer.Dispose()
}
# Keep the long source outside the test glob; only the requested durations are benchmarked.
[System.IO.File]::Move($source, (Join-Path $OutputDirectory 'benchmark-source.reference'), $true)
'Synthetic fixtures created locally; no microphone used.'
