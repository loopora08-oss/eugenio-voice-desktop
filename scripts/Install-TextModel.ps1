param([switch]$AllowExperimental)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$spec = Get-Content -LiteralPath (Join-Path $projectRoot 'models/text-model.json') -Raw | ConvertFrom-Json
if ($spec.releaseStatus -ne 'verified' -and -not $AllowExperimental) {
  throw 'Modello sperimentale: il controllo di qualità non è superato. Non è il modello consigliato per la distribuzione. Per riprodurre consapevolmente questo esperimento usa -AllowExperimental.'
}
$template = Get-Content -LiteralPath (Join-Path $projectRoot "models/$($spec.template)") -Raw
$handler = [Net.Http.HttpClientHandler]::new()
$handler.UseProxy = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.BaseAddress = [Uri]'http://127.0.0.1:11434/'
$client.Timeout = [TimeSpan]::FromMinutes(2)
try {
  $tags = $client.GetStringAsync('api/tags').GetAwaiter().GetResult() | ConvertFrom-Json
  $source = @($tags.models | Where-Object name -EQ $spec.source)
  if ($source.Count -ne 1) { throw "Modello pubblico mancante. Installa Ollama e acquisisci esplicitamente '$($spec.source)', poi ripeti. Lo script non scarica pesi né avvia servizi." }
  if ($source[0].digest -ne $spec.sourceDigest) { throw 'La versione del modello pubblico è diversa da quella verificata. Nessuna creazione eseguita: serve aggiornare e collaudare la specifica.' }
  $existing = @($tags.models | Where-Object name -EQ $spec.target)
  if ($existing.Count -gt 0) {
    if ($spec.PSObject.Properties.Name -contains 'targetDigest' -and $existing[0].digest -eq $spec.targetDigest) {
      Write-Output "Modello Eugenio già presente e verificato: $($spec.target) · $($existing[0].digest)"
      return
    }
    throw 'Esiste già un modello con il nome previsto. Nessuna sovrascrittura automatica: verificane identità e versione.'
  }
  $body = @{model=$spec.target;from=$spec.source;template=$template;system='';stream=$false;
    parameters=@{temperature=0;top_p=0.8;num_ctx=4096;num_thread=4;seed=42;stop=@('<|im_end|>','<|im_start|>','</s>')}} | ConvertTo-Json -Depth 5
  $content = [Net.Http.StringContent]::new($body, [Text.Encoding]::UTF8, 'application/json')
  try { $response = $client.PostAsync('api/create', $content).GetAwaiter().GetResult(); $response.EnsureSuccessStatusCode() | Out-Null }
  finally { $content.Dispose(); if ($null -ne $response) { $response.Dispose() } }
  $after = $client.GetStringAsync('api/tags').GetAwaiter().GetResult() | ConvertFrom-Json
  $created = @($after.models | Where-Object name -EQ $spec.target)
  if ($created.Count -ne 1 -or $created[0].digest -notmatch '^[a-f0-9]{64}$') { throw 'Creazione non confermata dal servizio locale. Nessuna impostazione di Eugenio è stata cambiata.' }
  if ($spec.PSObject.Properties.Name -contains 'targetDigest' -and $created[0].digest -ne $spec.targetDigest) { throw 'Il modello creato non coincide con il digest atteso. Non usarlo prima della verifica.' }
  Write-Output "Modello locale creato: $($spec.target) · digest $($created[0].digest). Pesi già presenti riutilizzati; nessun download, nessuna impostazione utente modificata."
}
finally { $client.Dispose() }
