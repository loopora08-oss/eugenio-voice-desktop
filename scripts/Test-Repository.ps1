param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
foreach ($script in @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' -File) + @(Get-Item -LiteralPath (Join-Path $projectRoot 'build.ps1'))) {
  $parseTokens = $null; $parseErrors = $null
  [System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$parseTokens, [ref]$parseErrors) | Out-Null
  if ($parseErrors.Count -gt 0) { throw "Sintassi PowerShell non valida: $($script.Name)" }
}
foreach ($file in @('LICENSE','README.md','global.json','compliance.toml','docs/ai-system-card.md','docs/roadmap-open-source.md','CONTRIBUTING.md')) {
  if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $file) -PathType Leaf)) { throw "File richiesto assente: $file" }
}
foreach ($file in @('CODE-SIGNING-POLICY.md','PRIVACY.md','signing/owned-binaries.json','signing/artifact-configuration.xml','.github/workflows/sign-windows.yml')) {
  if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $file) -PathType Leaf)) { throw "File di firma richiesto assente: $file" }
}
[xml]$buildProperties = Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw
if ($buildProperties.Project.PropertyGroup.Product -ne 'Eugenio') { throw 'Il nome prodotto deve essere Eugenio per i binari firmabili.' }
$compliance = (Get-Content -LiteralPath (Join-Path $projectRoot 'compliance.toml') -Raw).Replace("`r`n", "`n")
if ($compliance -notmatch '(?m)^uses_ai = true$' -or $compliance -notmatch '(?m)^decision = "go"' -or $compliance -notmatch 'system_card = "docs/ai-system-card.md"') {
  throw 'Triage IA assente o incoerente con il prodotto.'
}
$systemCard = Get-Content -LiteralPath (Join-Path $projectRoot 'docs/ai-system-card.md') -Raw
if ($systemCard -match '\bTODO\b|DA COMPILARE') { throw 'La system card contiene campi non compilati.' }
foreach ($project in @('App','Core','WhisperWorker','RewriteWorker','Tests','DesktopTests')) {
  $lock = Join-Path $projectRoot "$project/packages.lock.json"
  if (-not (Test-Path -LiteralPath $lock -PathType Leaf)) { throw "Lockfile assente: $project" }
  Get-Content -LiteralPath $lock -Raw | ConvertFrom-Json | Out-Null
}
$sdk = Get-Content -LiteralPath (Join-Path $projectRoot 'global.json') -Raw | ConvertFrom-Json
if ($sdk.sdk.version -notmatch '^10\.0\.\d+$') { throw 'La versione SDK non è puntuale.' }
$provenanceSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Core/RecognitionProvenance.cs') -Raw
if ($provenanceSource -notmatch 'WhisperEngineVersion = "Whisper.net/([0-9.]+)"') { throw 'Versione runtime nella provenienza non riconoscibile.' }
$declaredWhisperVersion = $Matches[1]
[xml]$whisperProject = Get-Content -LiteralPath (Join-Path $projectRoot 'WhisperWorker/WhisperWorker.csproj') -Raw
$whisperPackages = @($whisperProject.Project.ItemGroup.PackageReference | Where-Object { $_.Include -in @('Whisper.net','Whisper.net.Runtime') })
if ($whisperPackages.Count -ne 2 -or @($whisperPackages | Where-Object { $_.Version -ne $declaredWhisperVersion }).Count -gt 0) {
  throw 'La versione di provenienza non corrisponde alle dipendenze Whisper del worker.'
}
Write-Output 'PASS sintassi script, documentazione, triage, SDK e lockfile; non è un audit di sicurezza o conformità.'
