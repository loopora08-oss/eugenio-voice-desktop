param([Parameter(Mandatory)][string]$SourcePath)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$checker = Join-Path $PSScriptRoot 'Test-SigningPackage.ps1'
$fixtureRoot = Join-Path $repositoryRoot ('.artifacts/signing-checks-' + [Guid]::NewGuid().ToString('N'))
$baseline = Join-Path $fixtureRoot 'baseline'
New-Item -ItemType Directory -Path $baseline -Force | Out-Null
$source = (Resolve-Path -LiteralPath $SourcePath).Path
$owned = @(Get-Content -LiteralPath (Join-Path $repositoryRoot 'signing/owned-binaries.json') -Raw | ConvertFrom-Json)
foreach ($relative in $owned) {
  $destination = Join-Path $baseline $relative
  New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
  Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $destination
}
'Documento sintetico, nessun dato personale.' | Set-Content -LiteralPath (Join-Path $baseline 'note-fixture.txt') -Encoding utf8
$script:passed = 0
function ExpectFailure([string]$Name, [scriptblock]$Action, [string]$Pattern) {
  $rejected = $false
  try { & $Action | Out-Null }
  catch { if ($_.Exception.Message -notmatch $Pattern) { throw }; $rejected = $true }
  if (-not $rejected) { throw "Controllo negativo fallito: $Name" }
  $script:passed++; Write-Output "PASS $Name"
}
& $checker -Path $baseline -UnsignedPath $baseline | Out-Null
$script:passed++; Write-Output 'PASS metadati e pacchetto originale coerenti'
ExpectFailure 'pacchetto non firmato rifiutato come firmato' { & $checker -Path $baseline -UnsignedPath $baseline -RequireSignature -ExpectedSignerSubject 'CN=SignPath Foundation' } 'Firma assente, non valida o editore inatteso'
ExpectFailure 'richiesta senza editore atteso rifiutata' { & $checker -Path $baseline -UnsignedPath $baseline -RequireSignature } 'servono pacchetto originale ed editore atteso'
$missing = Join-Path $fixtureRoot 'missing'
Copy-Item -LiteralPath $baseline -Destination $missing -Recurse
Move-Item -LiteralPath (Join-Path $missing 'Core.dll') -Destination (Join-Path $missing 'Core.mancante')
ExpectFailure 'DLL propria mancante rifiutata' { & $checker -Path $missing } 'Binario proprio mancante'
$altered = Join-Path $fixtureRoot 'altered'
Copy-Item -LiteralPath $baseline -Destination $altered -Recurse
'Modifica estranea alla firma.' | Set-Content -LiteralPath (Join-Path $altered 'note-fixture.txt') -Encoding utf8
ExpectFailure 'alterazione del contenuto estraneo alla firma rifiutata' { & $checker -Path $altered -UnsignedPath $baseline } 'File estraneo alla firma modificato'
'File aggiunto dal servizio.' | Set-Content -LiteralPath (Join-Path $altered 'unexpected.txt') -Encoding utf8
ExpectFailure 'file aggiunto dal servizio rifiutato' { & $checker -Path $altered -UnsignedPath $baseline } 'aggiunto o rimosso file'
Write-Output "RESULT: $script:passed controlli passati. Nessuna firma reale o esecuzione dell'app effettuata."
